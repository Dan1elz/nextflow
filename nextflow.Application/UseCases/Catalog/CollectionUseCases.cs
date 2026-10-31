using System.Linq.Expressions;
using FluentValidation;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Collections;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Catalog;

internal static class CollectionRules
{
    public const string NameTakenMessage = "Já existe uma coleção com este nome.";

    /// <summary>Nome único, sem diferenciar maiúsculas (RN-CE40).</summary>
    public static Task<bool> NameTaken(ICollectionRepository repository, CollectionRequestDto dto, Guid? ignoredId, CancellationToken ct)
    {
        var name = dto.Name.Trim().ToLowerInvariant();
        return repository.Exists(c => c.Name.ToLower() == name && c.Id != ignoredId, ct);
    }
}

public class CreateCollectionUseCase(
    ICollectionRepository collectionRepository,
    IUnitOfWork unitOfWork,
    IValidator<CollectionRequestDto> validator,
    IResultFactory<CollectionResponseDto> resultFactory)
    : CreateUseCaseBase<Collection, ICollectionRepository, CollectionRequestDto, CollectionResponseDto>(
        collectionRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CollectionResponseDto>?> ValidateBusinessRules(CollectionRequestDto dto, CancellationToken ct)
        => await CollectionRules.NameTaken(_repository, dto, null, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CollectionRules.NameTakenMessage)
            : null;

    protected override Collection MapToEntity(CollectionRequestDto dto) => new(dto);

    protected override CollectionResponseDto MapToResponseDto(Collection entity) => new(entity);
}

public class UpdateCollectionUseCase(
    ICollectionRepository collectionRepository,
    IUnitOfWork unitOfWork,
    IValidator<CollectionRequestDto> validator,
    IResultFactory<CollectionResponseDto> resultFactory)
    : UpdateUseCaseBase<Collection, ICollectionRepository, CollectionRequestDto, CollectionResponseDto>(
        collectionRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CollectionResponseDto>?> ValidateBusinessRules(Collection entity, CollectionRequestDto dto, CancellationToken ct)
        => await CollectionRules.NameTaken(_repository, dto, entity.Id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CollectionRules.NameTakenMessage)
            : null;

    protected override CollectionResponseDto MapToResponseDto(Collection entity) => new(entity);
}

public class DeleteCollectionUseCase(
    ICollectionRepository collectionRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Collection, ICollectionRepository>(collectionRepository, unitOfWork, resultFactory);

public class ReactivateCollectionUseCase(
    ICollectionRepository collectionRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Collection, ICollectionRepository>(collectionRepository, unitOfWork, resultFactory);

public class GetCollectionByIdUseCase(
    ICollectionRepository collectionRepository,
    IResultFactory<CollectionResponseDto> resultFactory)
    : GetByIdUseCaseBase<Collection, ICollectionRepository, CollectionResponseDto>(collectionRepository, resultFactory)
{
    protected override CollectionResponseDto MapToResponseDto(Collection entity) => new(entity);
}

/// <summary>Listagem (T5). Filtros: <c>search</c> (nome) e <c>isActive</c>.</summary>
public class GetAllCollectionsUseCase(
    ICollectionRepository collectionRepository,
    IResultFactory<PaginationDto<CollectionResponseDto>> resultFactory)
    : GetAllUseCaseBase<Collection, ICollectionRepository, CollectionResponseDto>(collectionRepository, resultFactory)
{
    protected override Expression<Func<Collection, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Collection>().WhereBoolEquals(set, "isActive", c => c.IsActive);

        if (set.TryGetString("search", out var search))
        {
            var term = search.ToLowerInvariant();
            builder.And(c => c.Name.ToLower().Contains(term));
        }

        return builder.Predicate;
    }

    protected override Func<IQueryable<Collection>, IOrderedQueryable<Collection>> GetOrderBy()
        => q => q.OrderBy(c => c.Name).ThenBy(c => c.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<Collection, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Collection, object?>>>
        {
            ["name"] = c => c.Name,
            ["startsOn"] = c => c.StartsOn,
            ["endsOn"] = c => c.EndsOn,
        };

    protected override CollectionResponseDto MapToResponseDto(Collection entity) => new(entity);
}

/// <summary>Combo de coleção: só ativas (usado pelo produto na F1.3).</summary>
public class GetCollectionOptionsUseCase(
    ICollectionRepository collectionRepository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<Collection, ICollectionRepository, Option>(collectionRepository, resultFactory)
{
    protected override Expression<Func<Collection, bool>> BuildSearch(string term) => c => c.Name.ToLower().Contains(term);

    protected override Func<IQueryable<Collection>, IOrderedQueryable<Collection>> GetOrderBy()
        => q => q.OrderBy(c => c.Name).ThenBy(c => c.Id);

    protected override Option MapToOption(Collection entity) => new(entity.Id.ToString(), entity.Name);
}
