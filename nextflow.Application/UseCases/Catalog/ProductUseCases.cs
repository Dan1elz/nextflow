using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Domain.Validators.Catalog;

namespace Nextflow.Application.UseCases.Catalog;

/// <summary>Regras de escrita que consultam o banco, iguais em criar e editar. Devolvem as falhas por campo.</summary>
internal static class ProductRules
{
    /// <summary>Categoria existe e está ativa; a já vinculada pode estar inativa, para o produto seguir editável (RN-CE53).</summary>
    public static async Task<List<ValidationFailure>> Categories(
        ICategoryRepository categories, ProductRequestDto dto, ICollection<Guid> linked, CancellationToken ct)
    {
        var ids = dto.CategoryIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var found = (await categories.GetAll(c => ids.Contains(c.Id), 0, int.MaxValue, ct: ct)).ToList();
        var valid = ids.All(id => found.Any(c => c.Id == id && (c.IsActive || linked.Contains(id))));

        return valid ? [] : [new(nameof(dto.CategoryIds), "Escolha categorias ativas.")];
    }

    /// <summary>Coleção existe e está ativa, salvo a que o produto já tem.</summary>
    public static async Task<List<ValidationFailure>> Collection(
        ICollectionRepository collections, ProductRequestDto dto, Guid? current, CancellationToken ct)
    {
        if (dto.CollectionId is not { } id || id == current)
            return [];

        var collection = await collections.GetById(id, ct: ct);
        return collection is { IsActive: true } ? [] : [new(nameof(dto.CollectionId), "Escolha uma coleção ativa.")];
    }

    /// <summary>
    /// Só atributo, valor e variação do próprio produto entram por id: na criação, nenhum. Sem isso o id de outro produto
    /// viraria um registro novo com id escolhido pelo cliente.
    /// </summary>
    public static List<ValidationFailure> Ownership(ProductRequestDto dto, Product? product)
    {
        var failures = new List<ValidationFailure>();

        for (var a = 0; a < dto.Attributes.Count; a++)
        {
            var attributeDto = dto.Attributes[a];
            var attribute = product?.Attributes.FirstOrDefault(x => x.Id == attributeDto.Id);
            if (attributeDto.Id is not null && attribute is null)
                failures.Add(new($"Attributes[{a}].Id", "Este atributo não pertence ao produto."));

            for (var v = 0; v < attributeDto.Values.Count; v++)
            {
                var valueId = attributeDto.Values[v].Id;
                if (valueId is not null && attribute?.Values.Any(x => x.Id == valueId) != true)
                    failures.Add(new($"Attributes[{a}].Values[{v}].Id", "Este valor não pertence ao atributo."));
            }
        }

        for (var i = 0; i < dto.Variants.Count; i++)
        {
            var variantId = dto.Variants[i].Id;
            if (variantId is not null && product?.Variants.Any(x => x.Id == variantId) != true)
                failures.Add(new($"Variants[{i}].Id", "Esta variação não pertence ao produto."));
        }

        return failures;
    }

    /// <summary>
    /// SKU e código de barras únicos entre todos os produtos, ativos ou não, sem diferenciar maiúsculas (T2). Código de uma
    /// variação que está saindo do mesmo produto pode ser reaproveitado com a mesma grafia: aí o EF apaga antes de gravar. Com
    /// outra grafia ele não sabe da colisão, e o índice de lower(sku) recusaria o commit.
    /// </summary>
    public static async Task<List<ValidationFailure>> Codes(
        IProductVariantRepository variants, ProductRequestDto dto, Guid? productId, CancellationToken ct)
    {
        var skus = dto.Variants.Select(v => v.Sku.Trim().ToLower()).ToList();
        var barcodes = dto.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Barcode))
            .Select(v => v.Barcode!.Trim().ToLower())
            .ToList();

        var taken = (await variants.GetAll(
            v => skus.Contains(v.Sku.ToLower()) || (v.Barcode != null && barcodes.Contains(v.Barcode.ToLower())),
            0, int.MaxValue, ct: ct)).ToList();

        var kept = dto.Variants.Select(v => v.Id).ToHashSet();
        bool Conflicts(ProductVariant other, ProductVariantRequestDto variant, string? otherCode, string code)
            => other.Id != variant.Id && (other.ProductId != productId || kept.Contains(other.Id) || otherCode != code);

        var failures = new List<ValidationFailure>();
        for (var i = 0; i < dto.Variants.Count; i++)
        {
            var variant = dto.Variants[i];
            var sku = variant.Sku.Trim();
            var barcode = variant.Barcode?.Trim();

            if (taken.Any(o => string.Equals(o.Sku, sku, StringComparison.OrdinalIgnoreCase) && Conflicts(o, variant, o.Sku, sku)))
                failures.Add(new($"Variants[{i}].Sku", "Este SKU já está em uso."));

            if (!string.IsNullOrEmpty(barcode)
                && taken.Any(o => string.Equals(o.Barcode, barcode, StringComparison.OrdinalIgnoreCase) && Conflicts(o, variant, o.Barcode, barcode)))
                failures.Add(new($"Variants[{i}].Barcode", "Este código de barras já está em uso."));
        }

        return failures;
    }
}

public class CreateProductUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    IProductVariantRepository variantRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IUnitOfWork unitOfWork,
    IValidator<ProductRequestDto> validator,
    IResultFactory<ProductResponseDto> resultFactory)
    : CreateUseCaseBase<Product, IProductRepository, ProductRequestDto, ProductResponseDto>(
        productRepository, unitOfWork, validator, resultFactory)
{
    private Product? _saved;

    protected override async Task<ResultDto<ProductResponseDto>?> ValidateBusinessRules(ProductRequestDto dto, CancellationToken ct)
    {
        var failures = ProductRules.Ownership(dto, null);
        failures.AddRange(await ProductRules.Categories(categoryRepository, dto, [], ct));
        failures.AddRange(await ProductRules.Collection(collectionRepository, dto, null, ct));
        failures.AddRange(await ProductRules.Codes(variantRepository, dto, null, ct));

        return failures.Count == 0 ? null : _resultFactory.Failure(new ValidationResult(failures));
    }

    protected override Product MapToEntity(ProductRequestDto dto) => new(dto);

    protected override async Task AfterPersistence(Product entity, ProductRequestDto dto, CancellationToken ct)
        => _saved = await _repository.GetById(entity.Id, GetInclude(), ct);

    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var product = _saved ?? entity;
        return new(product, fileStorage.GetPublicUrl(product.ImageKey), pricing);
    }

    private static Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories).ThenInclude(c => c.Category).ThenInclude(c => c.Parent)
            .Include(p => p.Collection)
            .Include(p => p.Variants);
}

/// <summary>Trocar o tipo não apaga nada: ele só define padrões e campos da tela (RN-CE20, RN-CE55). Inativo não edita (RN-CE04, na base).</summary>
public class UpdateProductUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    IProductVariantRepository variantRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IUnitOfWork unitOfWork,
    IValidator<ProductRequestDto> validator,
    IResultFactory<ProductResponseDto> resultFactory)
    : UpdateUseCaseBase<Product, IProductRepository, ProductRequestDto, ProductResponseDto>(
        productRepository, unitOfWork, validator, resultFactory)
{
    private Product? _saved;

    protected override async Task<ResultDto<ProductResponseDto>?> ValidateBusinessRules(Product entity, ProductRequestDto dto, CancellationToken ct)
    {
        var failures = ProductRules.Ownership(dto, entity);
        failures.AddRange(await ProductRules.Categories(categoryRepository, dto, [.. entity.Categories.Select(c => c.CategoryId)], ct));
        failures.AddRange(await ProductRules.Collection(collectionRepository, dto, entity.CollectionId, ct));
        // F1.5 entra aqui: recusar a remoção de variação com saldo diferente de zero ou com movimentação (RN-CE51).
        failures.AddRange(await ProductRules.Codes(variantRepository, dto, entity.Id, ct));

        return failures.Count == 0 ? null : _resultFactory.Failure(new ValidationResult(failures));
    }

    protected override async Task AfterPersistence(Product entity, ProductRequestDto dto, CancellationToken ct)
        => _saved = await _repository.GetById(entity.Id, GetInclude(), ct);

    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var product = _saved ?? entity;
        return new(product, fileStorage.GetPublicUrl(product.ImageKey), pricing);
    }

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories).ThenInclude(c => c.Category).ThenInclude(c => c.Parent)
            .Include(p => p.Collection)
            .Include(p => p.Attributes).ThenInclude(a => a.Values)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues);
}

public class DeleteProductUseCase(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Product, IProductRepository>(productRepository, unitOfWork, resultFactory);

public class ReactivateProductUseCase(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Product, IProductRepository>(productRepository, unitOfWork, resultFactory);

public class GetProductByIdUseCase(
    IProductRepository productRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IResultFactory<ProductResponseDto> resultFactory)
    : GetByIdUseCaseBase<Product, IProductRepository, ProductResponseDto>(productRepository, resultFactory)
{
    protected override ProductResponseDto MapToResponseDto(Product entity)
        => new(entity, fileStorage.GetPublicUrl(entity.ImageKey), pricing);

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories).ThenInclude(c => c.Category).ThenInclude(c => c.Parent)
            .Include(p => p.Collection)
            .Include(p => p.Variants);
}

/// <summary>Aba Variações do T3: as variações do produto, na ordem da grade.</summary>
public class GetProductVariantsUseCase(
    IProductRepository productRepository,
    IProductPricingService pricing,
    IResultFactory<List<ProductVariantResponseDto>> resultFactory)
    : GetByIdUseCaseBase<Product, IProductRepository, List<ProductVariantResponseDto>>(productRepository, resultFactory)
{
    protected override List<ProductVariantResponseDto> MapToResponseDto(Product entity)
        => [.. entity.Variants.OrderBy(v => v.SortOrder).Select(v => new ProductVariantResponseDto(v, pricing))];

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Variants)
            .ThenInclude(v => v.AttributeValues)
            .ThenInclude(link => link.AttributeValue)
            .ThenInclude(value => value.Attribute);
}

/// <summary>Listagem (T1). Filtros: search, categoryId (nível 1 traz as filhas), productType, collectionId, lifecycle, isActive.</summary>
public class GetAllProductsUseCase(
    IProductRepository productRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IResultFactory<PaginationDto<ProductListItemDto>> resultFactory)
    : GetAllUseCaseBase<Product, IProductRepository, ProductListItemDto>(productRepository, resultFactory)
{
    protected override Expression<Func<Product, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Product>()
            .WhereBoolEquals(set, "isActive", p => p.IsActive)
            .WhereEnumEquals(set, "productType", p => p.ProductType)
            .WhereEnumEquals(set, "lifecycle", p => p.Lifecycle)
            .WhereGuidEquals(set, "collectionId", p => p.CollectionId);

        if (set.TryGetString("search", out var search))
        {
            var term = search.ToLowerInvariant();
            builder.And(p => p.Name.ToLower().Contains(term)
                || (p.Brand != null && p.Brand.ToLower().Contains(term))
                || p.Variants.Any(v => v.Sku.ToLower().Contains(term)
                    || (v.Barcode != null && v.Barcode.ToLower().Contains(term))));
        }

        if (set.TryGetString("categoryId", out var raw) && Guid.TryParse(raw, out var categoryId))
            builder.And(p => p.Categories.Any(c => c.CategoryId == categoryId || c.Category.ParentId == categoryId));

        return builder.Predicate;
    }

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories).ThenInclude(c => c.Category).ThenInclude(c => c.Parent)
            .Include(p => p.Variants);

    protected override Func<IQueryable<Product>, IOrderedQueryable<Product>> GetOrderBy()
        => q => q.OrderBy(p => p.Name).ThenBy(p => p.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<Product, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Product, object?>>>
        {
            ["name"] = p => p.Name,
            // F2.3: ordena pelo preço de tabela enquanto preço efetivo = preço da variação.
            ["price"] = p => p.Variants.Where(v => v.IsActive).Min(v => (decimal?)v.Price),
        };

    protected override ProductListItemDto MapToResponseDto(Product entity)
        => new(entity, fileStorage.GetPublicUrl(entity.ImageKey), pricing);
}

/// <summary>Monta o formulário T2. As opções trazem as ativas e as já vinculadas, para o combo mostrar o que está gravado.</summary>
public class GetProductFormUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    IFileStorage fileStorage,
    IResultFactory<ProductFormDto> resultFactory) : IGetProductFormUseCase
{
    public async Task<ResultDto<ProductFormDto>> Execute(Guid? id, CancellationToken ct = default)
    {
        Product? product = null;
        if (id is { } productId)
        {
            product = await productRepository.GetById(productId, GetInclude(), ct);
            if (product is null)
                return resultFactory.Failure($"Entidade com id {productId} não encontrada.", 404);
        }

        var linkedCategories = product?.Categories.Select(c => c.CategoryId).ToList() ?? [];
        var linkedCollection = product?.CollectionId;

        var categories = await categoryRepository.GetFormOptions(linkedCategories, ct);
        var collections = await collectionRepository.GetAll(
            c => c.IsActive || c.Id == linkedCollection,
            0,
            int.MaxValue,
            orderBy: q => q.OrderBy(c => c.Name).ThenBy(c => c.Id),
            ct: ct);

        var options = new ProductFormOptionsDto
        {
            Categories = [.. categories.Select(c => new CategoryOptionDto(c))],
            Collections = [.. collections.Select(c => new Option(c.Id.ToString(), c.Name))],
        };

        return resultFactory.Success(product is null
            ? new ProductFormDto(options)
            : new ProductFormDto(product, fileStorage.GetPublicUrl(product.ImageKey), options));
    }

    private static Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories)
            .Include(p => p.Attributes).ThenInclude(a => a.Values)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues);
}

/// <summary>Marca em linha ou fora de linha (RN-CE19). Produto inativo não é editado (RN-CE04).</summary>
public class UpdateProductLifecycleUseCase(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IValidator<ProductLifecycleRequestDto> validator,
    IResultFactory<bool> resultFactory) : IUpdateProductLifecycleUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid id, ProductLifecycleRequestDto dto, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var product = await productRepository.GetByIdForUpdate(id, ct: ct);
        if (product is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);
        if (!product.IsActive)
            return resultFactory.Failure("Produto está inativo e não pode ser alterado.", 400);

        product.ChangeLifecycle(dto.Lifecycle);
        await unitOfWork.CommitAsync(ct);

        return resultFactory.Success(true);
    }
}

/// <summary>
/// Sobe a imagem nova, grava no banco e só então apaga a antiga (ADR 0006). Se o banco falhar, a nova é apagada; falha ou
/// cancelamento ao apagar a antiga só deixa um arquivo sobrando e não desfaz a troca, que já está gravada.
/// </summary>
public class UploadProductImageUseCase(
    IProductRepository productRepository,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork,
    IValidator<UploadFileDto> validator,
    IResultFactory<ProductImageResponseDto> resultFactory,
    ILogger<UploadProductImageUseCase> logger) : IUploadProductImageUseCase
{
    public async Task<ResultDto<ProductImageResponseDto>> Execute(Guid id, UploadFileDto? file, CancellationToken ct = default)
    {
        if (file is null)
            return resultFactory.FieldFailure(UploadFileDtoValidator.Field, "A imagem é obrigatória.");

        var validation = await validator.ValidateAsync(file, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var product = await productRepository.GetByIdForUpdate(id, ct: ct);
        if (product is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);
        if (!product.IsActive)
            return resultFactory.Failure("Produto está inativo e não pode ser alterado.", 400);

        var previousKey = product.ImageKey;
        var newKey = await fileStorage.UploadAsync(StorageFolder.ProductImages, file, ct);

        try
        {
            product.SetImage(newKey);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await fileStorage.DeleteAsync(StorageFolder.ProductImages.Bucket, newKey, CancellationToken.None);
            throw;
        }

        if (previousKey is not null)
            await DeletePrevious(previousKey, id, ct);

        return resultFactory.Success(new ProductImageResponseDto(fileStorage.GetPublicUrl(newKey)));
    }

    private async Task DeletePrevious(string key, Guid productId, CancellationToken ct)
    {
        try
        {
            await fileStorage.DeleteAsync(StorageFolder.ProductImages.Bucket, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível apagar a imagem antiga {Key} do produto {ProductId}.", key, productId);
        }
    }
}

/// <summary>
/// Grava no banco antes de apagar: se o banco falhar, o produto continua apontando para um arquivo que existe. Falha ou
/// cancelamento depois do commit só deixa um arquivo sobrando: a remoção já está gravada e a resposta é sucesso.
/// </summary>
public class RemoveProductImageUseCase(
    IProductRepository productRepository,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory,
    ILogger<RemoveProductImageUseCase> logger) : IRemoveProductImageUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid id, CancellationToken ct = default)
    {
        var product = await productRepository.GetByIdForUpdate(id, ct: ct);
        if (product is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);
        if (!product.IsActive)
            return resultFactory.Failure("Produto está inativo e não pode ser alterado.", 400);
        if (product.ImageKey is not { } key)
            return resultFactory.Success(true);

        product.RemoveImage();
        await unitOfWork.CommitAsync(ct);

        try
        {
            await fileStorage.DeleteAsync(StorageFolder.ProductImages.Bucket, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível apagar a imagem {Key} do produto {ProductId}.", key, id);
        }

        return resultFactory.Success(true);
    }
}
