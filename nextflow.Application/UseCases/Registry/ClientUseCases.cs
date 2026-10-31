using System.Linq.Expressions;
using FluentValidation;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Application.UseCases.Registry;

public class CreateClientUseCase(
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork,
    IValidator<ClientRequestDto> validator,
    IResultFactory<ClientResponseDto> resultFactory)
    : CreateUseCaseBase<Client, IClientRepository, ClientRequestDto, ClientResponseDto>(
        clientRepository, unitOfWork, validator, resultFactory)
{
    /// <summary>CPF único quando informado (RN-CD01, L-CP6): erro no campo.</summary>
    protected override async Task<ResultDto<ClientResponseDto>?> ValidateBusinessRules(ClientRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Cpf))
            return null;

        var cpf = TaxDocument.ParseCpf(dto.Cpf);
        return await _repository.Exists(c => c.Cpf == cpf, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cpf), "Já existe um cliente com este CPF.")
            : null;
    }

    protected override Client MapToEntity(ClientRequestDto dto) => new(dto);

    protected override ClientResponseDto MapToResponseDto(Client entity) => new(entity);
}

/// <summary>Cadastro rápido (T-CD4): cliente e telefone principal no mesmo commit.</summary>
public class QuickCreateClientUseCase(
    IClientRepository clientRepository,
    IContactRepository contactRepository,
    IUnitOfWork unitOfWork,
    IValidator<QuickCreateClientRequestDto> validator,
    IResultFactory<ClientOptionDto> resultFactory)
    : CreateUseCaseBase<Client, IClientRepository, QuickCreateClientRequestDto, ClientOptionDto>(
        clientRepository, unitOfWork, validator, resultFactory)
{
    private string? _phone;

    protected override Client MapToEntity(QuickCreateClientRequestDto dto) => new(dto);

    protected override async Task BeforePersistence(Client entity, QuickCreateClientRequestDto dto, CancellationToken ct)
    {
        var contact = new Contact(new ContactRequestDto
        {
            OwnerType = OwnerTypeEnum.Client,
            OwnerId = entity.Id,
            Type = ContactTypeEnum.Phone,
            Value = dto.Phone,
            IsMain = true,
        });
        _phone = contact.Value;
        await contactRepository.AddAsync(contact, ct);
    }

    protected override ClientOptionDto MapToResponseDto(Client entity) => new(entity, _phone);
}

public class UpdateClientUseCase(
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork,
    IValidator<ClientRequestDto> validator,
    IResultFactory<ClientResponseDto> resultFactory)
    : UpdateUseCaseBase<Client, IClientRepository, ClientRequestDto, ClientResponseDto>(
        clientRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<ClientResponseDto>?> ValidateBusinessRules(Client entity, ClientRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Cpf))
            return null;

        var cpf = TaxDocument.ParseCpf(dto.Cpf);
        var id = entity.Id;
        return await _repository.Exists(c => c.Cpf == cpf && c.Id != id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cpf), "Já existe um cliente com este CPF.")
            : null;
    }

    protected override ClientResponseDto MapToResponseDto(Client entity) => new(entity);
}

public class DeleteClientUseCase(
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Client, IClientRepository>(clientRepository, unitOfWork, resultFactory);

public class ReactivateClientUseCase(
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Client, IClientRepository>(clientRepository, unitOfWork, resultFactory);

public class GetClientByIdUseCase(
    IClientRepository clientRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<ClientResponseDto> resultFactory)
    : GetByIdUseCaseBase<Client, IClientRepository, ClientResponseDto>(clientRepository, resultFactory)
{
    public override async Task<ResultDto<ClientResponseDto>> Execute(Guid id, CancellationToken ct = default)
    {
        var result = await base.Execute(id, ct);
        if (result.IsSuccess)
        {
            result.Data!.MainAddress = await ownerDirectory.GetMainAddress(OwnerTypeEnum.Client, id, ct);
            result.Data.MainContact = await ownerDirectory.GetMainContact(OwnerTypeEnum.Client, id, ct);
        }
        return result;
    }

    protected override ClientResponseDto MapToResponseDto(Client entity) => new(entity);
}

/// <summary>
/// Listagem (T-CD3). Filtros: <c>search</c> (nome, CPF, telefone), <c>isActive</c> e <c>birthdaysThisMonth</c>
/// (aniversariantes no mês da data de negócio, L-CP7).
/// </summary>
public class GetAllClientsUseCase(
    IClientRepository clientRepository,
    IContactRepository contactRepository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IResultFactory<PaginationDto<ClientListItemDto>> resultFactory)
    : GetAllUseCaseBase<Client, IClientRepository, ClientListItemDto>(clientRepository, resultFactory)
{
    private List<Guid> _ownersByPhone = [];
    private IReadOnlyDictionary<Guid, OwnerSummaryDto> _summaries = new Dictionary<Guid, OwnerSummaryDto>();

    public override async Task<ResultDto<PaginationDto<ClientListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        if (new FilterSet(filters).TryGetString("search", out var search) && Digits.Only(search) is { Length: > 0 } digits)
            _ownersByPhone = await contactRepository.GetOwnerIdsByPhone(OwnerTypeEnum.Client, digits, ct);

        return await base.Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<Client, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Client>().WhereBoolEquals(set, "isActive", c => c.IsActive);

        if (set.TryGetString("search", out var search))
        {
            builder.And(ClientSearch.By(search, _ownersByPhone));
        }

        if (set.TryGetString("birthdaysThisMonth", out var birthdays) && bool.TryParse(birthdays, out var onlyBirthdays) && onlyBirthdays)
        {
            var month = clock.Today.Month;
            builder.And(c => c.BirthDate != null && c.BirthDate.Value.Month == month);
        }

        return builder.Predicate;
    }

    protected override async Task PrepareMapping(IReadOnlyList<Client> items, CancellationToken ct)
        => _summaries = await ownerDirectory.GetSummaries(OwnerTypeEnum.Client, [.. items.Select(c => c.Id)], ct);

    protected override Func<IQueryable<Client>, IOrderedQueryable<Client>> GetOrderBy()
        => q => q.OrderBy(c => c.Name).ThenBy(c => c.LastName).ThenBy(c => c.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<Client, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Client, object?>>>
        {
            ["fullName"] = c => c.Name,
            ["birthDate"] = c => c.BirthDate,
            ["createdAt"] = c => c.CreatedAt,
        };

    protected override ClientListItemDto MapToResponseDto(Client entity) => new(entity, _summaries.GetValueOrDefault(entity.Id));
}

/// <summary>Combo de cliente na venda: só ativos, com CPF mascarado e telefone (T-CD4).</summary>
public class GetClientOptionsUseCase(
    IClientRepository clientRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<List<ClientOptionDto>> resultFactory)
    : GetOptionsUseCaseBase<Client, IClientRepository, ClientOptionDto>(clientRepository, resultFactory)
{
    private IReadOnlyDictionary<Guid, OwnerSummaryDto> _summaries = new Dictionary<Guid, OwnerSummaryDto>();

    protected override Expression<Func<Client, bool>> BuildSearch(string term) => ClientSearch.By(term);

    protected override Func<IQueryable<Client>, IOrderedQueryable<Client>> GetOrderBy()
        => q => q.OrderBy(c => c.Name).ThenBy(c => c.LastName).ThenBy(c => c.Id);

    protected override async Task PrepareMapping(IReadOnlyList<Client> items, CancellationToken ct)
        => _summaries = await ownerDirectory.GetSummaries(OwnerTypeEnum.Client, [.. items.Select(c => c.Id)], ct);

    protected override ClientOptionDto MapToOption(Client entity)
        => new(entity, _summaries.GetValueOrDefault(entity.Id)?.MainPhone);
}

internal static class ClientSearch
{
    /// <summary>
    /// Nome, sobrenome ou dígitos do CPF. O cast por <c>object</c> faz o EF comparar a coluna de texto em vez do value
    /// object: só vale traduzido para SQL.
    /// </summary>
    public static Expression<Func<Client, bool>> By(string search, List<Guid>? ownersByPhone = null)
    {
        var owners = ownersByPhone ?? [];
        var term = search.Trim().ToLowerInvariant();
        var digits = Digits.Only(term);
        return digits.Length > 0
            ? c => c.Name.ToLower().Contains(term) || (c.LastName != null && c.LastName.ToLower().Contains(term))
                || (c.Cpf != null && ((string)(object)c.Cpf).Contains(digits)) || owners.Contains(c.Id)
            : c => c.Name.ToLower().Contains(term) || (c.LastName != null && c.LastName.ToLower().Contains(term));
    }
}
