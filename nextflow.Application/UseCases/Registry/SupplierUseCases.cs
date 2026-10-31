using System.Linq.Expressions;
using FluentValidation;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Application.UseCases.Registry;

public class CreateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IValidator<SupplierRequestDto> validator,
    IResultFactory<SupplierResponseDto> resultFactory)
    : CreateUseCaseBase<Supplier, ISupplierRepository, SupplierRequestDto, SupplierResponseDto>(
        supplierRepository, unitOfWork, validator, resultFactory)
{
    /// <summary>CNPJ único (RN-CD02): erro no campo.</summary>
    protected override async Task<ResultDto<SupplierResponseDto>?> ValidateBusinessRules(SupplierRequestDto dto, CancellationToken ct)
    {
        var cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        return await _repository.Exists(s => s.Cnpj == cnpj, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cnpj), "Já existe um fornecedor com este CNPJ.")
            : null;
    }

    protected override Supplier MapToEntity(SupplierRequestDto dto) => new(dto);

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

public class UpdateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IValidator<SupplierRequestDto> validator,
    IResultFactory<SupplierResponseDto> resultFactory)
    : UpdateUseCaseBase<Supplier, ISupplierRepository, SupplierRequestDto, SupplierResponseDto>(
        supplierRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<SupplierResponseDto>?> ValidateBusinessRules(Supplier entity, SupplierRequestDto dto, CancellationToken ct)
    {
        var cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        var id = entity.Id;
        return await _repository.Exists(s => s.Cnpj == cnpj && s.Id != id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cnpj), "Já existe um fornecedor com este CNPJ.")
            : null;
    }

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

public class DeleteSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Supplier, ISupplierRepository>(supplierRepository, unitOfWork, resultFactory);

public class ReactivateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Supplier, ISupplierRepository>(supplierRepository, unitOfWork, resultFactory);

public class GetSupplierByIdUseCase(
    ISupplierRepository supplierRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<SupplierResponseDto> resultFactory)
    : GetByIdUseCaseBase<Supplier, ISupplierRepository, SupplierResponseDto>(supplierRepository, resultFactory)
{
    public override async Task<ResultDto<SupplierResponseDto>> Execute(Guid id, CancellationToken ct = default)
    {
        var result = await base.Execute(id, ct);
        if (result.IsSuccess)
        {
            result.Data!.MainAddress = await ownerDirectory.GetMainAddress(OwnerTypeEnum.Supplier, id, ct);
            result.Data.MainContact = await ownerDirectory.GetMainContact(OwnerTypeEnum.Supplier, id, ct);
        }
        return result;
    }

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

/// <summary>Listagem (T-CD1). Filtros: <c>search</c> (nome, CNPJ), <c>isActive</c>, <c>cityId</c>.</summary>
public class GetAllSuppliersUseCase(
    ISupplierRepository supplierRepository,
    IAddressRepository addressRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<PaginationDto<SupplierListItemDto>> resultFactory)
    : GetAllUseCaseBase<Supplier, ISupplierRepository, SupplierListItemDto>(supplierRepository, resultFactory)
{
    private List<Guid>? _ownersInCity;
    private IReadOnlyDictionary<Guid, OwnerSummaryDto> _summaries = new Dictionary<Guid, OwnerSummaryDto>();

    public override async Task<ResultDto<PaginationDto<SupplierListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        if (new FilterSet(filters).TryGetString("cityId", out var raw) && Guid.TryParse(raw, out var cityId))
            _ownersInCity = await addressRepository.GetOwnerIdsByCity(OwnerTypeEnum.Supplier, cityId, ct);

        return await base.Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<Supplier, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Supplier>().WhereBoolEquals(set, "isActive", s => s.IsActive);

        if (set.TryGetString("search", out var search))
            builder.And(SupplierSearch.By(search));

        if (_ownersInCity is { } owners)
            builder.And(s => owners.Contains(s.Id));

        return builder.Predicate;
    }

    protected override async Task PrepareMapping(IReadOnlyList<Supplier> items, CancellationToken ct)
        => _summaries = await ownerDirectory.GetSummaries(OwnerTypeEnum.Supplier, [.. items.Select(s => s.Id)], ct);

    protected override Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<Supplier, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Supplier, object?>>>
        {
            ["name"] = s => s.Name,
            ["leadTimeDays"] = s => s.LeadTimeDays,
            ["createdAt"] = s => s.CreatedAt,
        };

    protected override SupplierListItemDto MapToResponseDto(Supplier entity) => new(entity, _summaries.GetValueOrDefault(entity.Id));
}

/// <summary>Combo de fornecedor: só ativos, label "nome · CNPJ" (T-CD2).</summary>
public class GetSupplierOptionsUseCase(
    ISupplierRepository supplierRepository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<Supplier, ISupplierRepository, Option>(supplierRepository, resultFactory)
{
    protected override Expression<Func<Supplier, bool>> BuildSearch(string term) => SupplierSearch.By(term);

    protected override Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override Option MapToOption(Supplier entity) => new(entity.Id.ToString(), $"{entity.Name} · {entity.Cnpj.Masked}");
}

internal static class SupplierSearch
{
    /// <summary>
    /// Nome ou dígitos do CNPJ. O cast por <c>object</c> faz o EF comparar a coluna de texto em vez do value object:
    /// só vale traduzido para SQL.
    /// </summary>
    public static Expression<Func<Supplier, bool>> By(string search)
    {
        var term = search.Trim().ToLowerInvariant();
        var digits = Digits.Only(term);
        return digits.Length > 0
            ? s => s.Name.ToLower().Contains(term) || ((string)(object)s.Cnpj).Contains(digits)
            : s => s.Name.ToLower().Contains(term);
    }
}
