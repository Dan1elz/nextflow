using System.Linq.Expressions;
using FluentValidation;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

public class CreateContactUseCase(
    IContactRepository contactRepository,
    IOwnerDirectory ownerDirectory,
    IUnitOfWork unitOfWork,
    IValidator<ContactRequestDto> validator,
    IResultFactory<ContactResponseDto> resultFactory)
    : CreateUseCaseBase<Contact, IContactRepository, ContactRequestDto, ContactResponseDto>(
        contactRepository, unitOfWork, validator, resultFactory)
{
    protected override Task<ResultDto<ContactResponseDto>?> ValidateBusinessRules(ContactRequestDto dto, CancellationToken ct)
        => OwnerRules.CheckOwner(ownerDirectory, _resultFactory, dto.OwnerType, dto.OwnerId, ct);

    protected override Contact MapToEntity(ContactRequestDto dto) => new(dto);

    protected override Task BeforePersistence(Contact entity, ContactRequestDto dto, CancellationToken ct)
        => OwnerRules.ApplyMainFlagOnCreate(_repository, entity, ct);

    protected override ContactResponseDto MapToResponseDto(Contact entity) => new(entity);
}

public class UpdateContactUseCase(
    IContactRepository contactRepository,
    IOwnerDirectory ownerDirectory,
    IUnitOfWork unitOfWork,
    IValidator<UpdateContactRequestDto> validator,
    IResultFactory<ContactResponseDto> resultFactory)
    : UpdateUseCaseBase<Contact, IContactRepository, UpdateContactRequestDto, ContactResponseDto>(
        contactRepository, unitOfWork, validator, resultFactory)
{
    protected override Task<ResultDto<ContactResponseDto>?> ValidateBusinessRules(Contact entity, UpdateContactRequestDto dto, CancellationToken ct)
        => OwnerRules.CheckOwner(ownerDirectory, _resultFactory, entity.OwnerType, entity.OwnerId, ct);

    protected override Task BeforePersistence(Contact entity, UpdateContactRequestDto dto, CancellationToken ct)
        => OwnerRules.ApplyMainFlagOnUpdate(_repository, entity, ct);

    protected override ContactResponseDto MapToResponseDto(Contact entity) => new(entity);
}

public class DeleteContactUseCase(
    IContactRepository contactRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Contact, IContactRepository>(contactRepository, unitOfWork, resultFactory);

/// <summary>Contatos de um dono: <c>ownerType</c> e <c>ownerId</c> são obrigatórios (decisão 4 de 29/10).</summary>
public class GetAllContactsUseCase(
    IContactRepository contactRepository,
    IResultFactory<PaginationDto<ContactResponseDto>> resultFactory)
    : GetAllUseCaseBase<Contact, IContactRepository, ContactResponseDto>(contactRepository, resultFactory)
{
    public override Task<ResultDto<PaginationDto<ContactResponseDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
        => OwnerRules.TryGetOwner(filters, out _, out _)
            ? base.Execute(offset, limit, filters, sort, order, ct)
            : Task.FromResult(_resultFactory.Failure("Informe ownerType e ownerId nos filtros.", 400));

    protected override Expression<Func<Contact, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        OwnerRules.TryGetOwner(filters, out var ownerType, out var ownerId);
        return c => c.OwnerType == ownerType && c.OwnerId == ownerId;
    }

    protected override Func<IQueryable<Contact>, IOrderedQueryable<Contact>> GetOrderBy()
        => q => q.OrderByDescending(c => c.IsMain).ThenBy(c => c.CreatedAt);

    protected override ContactResponseDto MapToResponseDto(Contact entity) => new(entity);
}
