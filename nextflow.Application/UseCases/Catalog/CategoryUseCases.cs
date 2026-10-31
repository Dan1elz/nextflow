using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Catalog;

internal static class CategoryRules
{
    public const string NameTakenMessage = "Já existe uma categoria com este nome neste nível.";

    /// <summary>Pai precisa existir, ser de nível 1 (RN-CE23) e estar ativo (RN-CE39). Devolve o erro do campo, ou nulo.</summary>
    public static async Task<string?> ParentError(ICategoryRepository repository, Guid parentId, CancellationToken ct)
    {
        var parent = await repository.GetById(parentId, ct: ct);
        if (parent is null)
            return "Categoria pai não encontrada.";
        if (parent.ParentId is not null)
            return "Uma subcategoria não pode ter subcategorias: escolha uma categoria de nível 1.";
        return parent.IsActive ? null : "A categoria pai está inativa.";
    }

    /// <summary>Nome único entre irmãs, sem diferenciar maiúsculas (RN-CE38).</summary>
    public static Task<bool> NameTaken(ICategoryRepository repository, CategoryRequestDto dto, Guid? ignoredId, CancellationToken ct)
    {
        var name = dto.Name.Trim().ToLowerInvariant();
        var parentId = dto.ParentId;
        return repository.Exists(c => c.ParentId == parentId && c.Name.ToLower() == name && c.Id != ignoredId, ct);
    }
}

public class CreateCategoryUseCase(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IValidator<CategoryRequestDto> validator,
    IResultFactory<CategoryResponseDto> resultFactory)
    : CreateUseCaseBase<Category, ICategoryRepository, CategoryRequestDto, CategoryResponseDto>(
        categoryRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CategoryResponseDto>?> ValidateBusinessRules(CategoryRequestDto dto, CancellationToken ct)
    {
        if (dto.ParentId is { } parentId && await CategoryRules.ParentError(_repository, parentId, ct) is { } parentError)
            return _resultFactory.FieldFailure(nameof(dto.ParentId), parentError);

        return await CategoryRules.NameTaken(_repository, dto, null, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CategoryRules.NameTakenMessage)
            : null;
    }

    protected override Category MapToEntity(CategoryRequestDto dto) => new(dto);

    protected override CategoryResponseDto MapToResponseDto(Category entity) => new(entity);
}

public class UpdateCategoryUseCase(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IValidator<CategoryRequestDto> validator,
    IResultFactory<CategoryResponseDto> resultFactory)
    : UpdateUseCaseBase<Category, ICategoryRepository, CategoryRequestDto, CategoryResponseDto>(
        categoryRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<CategoryResponseDto>?> ValidateBusinessRules(Category entity, CategoryRequestDto dto, CancellationToken ct)
    {
        var id = entity.Id;

        if (dto.ParentId is { } parentId)
        {
            if (parentId == id)
                return _resultFactory.FieldFailure(nameof(dto.ParentId), "Uma categoria não pode ser pai dela mesma.");

            // RN-CE23: quem tem filhas viraria avó, mesmo com a filha inativa.
            if (await _repository.Exists(c => c.ParentId == id, ct))
                return _resultFactory.FieldFailure(nameof(dto.ParentId), "Esta categoria tem subcategorias e não pode virar subcategoria.");

            if (await CategoryRules.ParentError(_repository, parentId, ct) is { } parentError)
                return _resultFactory.FieldFailure(nameof(dto.ParentId), parentError);
        }

        return await CategoryRules.NameTaken(_repository, dto, id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Name), CategoryRules.NameTakenMessage)
            : null;
    }

    protected override CategoryResponseDto MapToResponseDto(Category entity) => new(entity);
}

/// <summary>Inativa a categoria. Com subcategoria ativa, não sai (RN-CE39). "Com produtos ativos pede confirmação" entra na F1.3.</summary>
public class DeleteCategoryUseCase(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Category, ICategoryRepository>(categoryRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(Category entity, Guid? userId, CancellationToken ct)
    {
        var id = entity.Id;
        return await _repository.Exists(c => c.ParentId == id && c.IsActive, ct)
            ? _resultFactory.Failure("Inative as subcategorias antes de inativar esta categoria.", 400)
            : null;
    }
}

/// <summary>Reativa a categoria. Subcategoria de pai inativo não volta antes do pai (RN-CE39).</summary>
public class ReactivateCategoryUseCase(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Category, ICategoryRepository>(categoryRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(Category entity, Guid? userId, CancellationToken ct)
    {
        if (entity.ParentId is not { } parentId)
            return null;

        var parent = await _repository.GetById(parentId, ct: ct);
        return parent is { IsActive: true }
            ? null
            : _resultFactory.Failure("Reative a categoria pai antes de reativar esta subcategoria.", 400);
    }
}

public class GetCategoryByIdUseCase(
    ICategoryRepository categoryRepository,
    IResultFactory<CategoryResponseDto> resultFactory)
    : GetByIdUseCaseBase<Category, ICategoryRepository, CategoryResponseDto>(categoryRepository, resultFactory)
{
    protected override CategoryResponseDto MapToResponseDto(Category entity) => new(entity);
}

/// <summary>Árvore da tela T4, com as inativas: nível 1 e filhas, por nome.</summary>
public class GetCategoryTreeUseCase(
    ICategoryRepository categoryRepository,
    IResultFactory<List<CategoryNodeDto>> resultFactory) : IGetCategoryTreeUseCase
{
    public async Task<ResultDto<List<CategoryNodeDto>>> Execute(CancellationToken ct = default)
    {
        var roots = await categoryRepository.GetAll(
            c => c.ParentId == null,
            0,
            int.MaxValue,
            GetInclude(),
            q => q.OrderBy(c => c.Name).ThenBy(c => c.Id),
            ct);

        return resultFactory.Success([.. roots.Select(root => new CategoryNodeDto(root))]);
    }

    private static Func<IQueryable<Category>, IQueryable<Category>> GetInclude()
        => q => q.Include(c => c.Children.OrderBy(child => child.Name));
}

/// <summary>Combo de categoria: só ativas, pai antes das filhas. O filtro <c>rootsOnly</c> deixa só o nível 1 (RN-CE23).</summary>
public class GetCategoryOptionsUseCase(
    ICategoryRepository categoryRepository,
    IResultFactory<List<CategoryOptionDto>> resultFactory)
    : GetOptionsUseCaseBase<Category, ICategoryRepository, CategoryOptionDto>(categoryRepository, resultFactory)
{
    protected override Expression<Func<Category, bool>> BuildSearch(string term) => c => c.Name.ToLower().Contains(term);

    protected override Func<IQueryable<Category>, IOrderedQueryable<Category>> GetOrderBy()
        => q => q.OrderBy(c => c.Parent != null ? c.Parent.Name : c.Name)
            .ThenBy(c => c.ParentId != null)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id);

    protected override void ApplyFilters(FilterExpressionBuilder<Category> builder, FilterSet filters)
    {
        if (filters.TryGetString("rootsOnly", out var rootsOnly) && bool.TryParse(rootsOnly, out var value) && value)
            builder.And(c => c.ParentId == null);
    }

    protected override Func<IQueryable<Category>, IQueryable<Category>> GetInclude() => q => q.Include(c => c.Parent);

    protected override CategoryOptionDto MapToOption(Category entity) => new(entity);
}
