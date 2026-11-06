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
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Dtos.Suppliers;
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
    public const string UserRequiredMessage = "Usuário não identificado: o histórico de preço exige quem alterou.";

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
            // Variação removida não volta por id: saiu do formulário de vez, e só o histórico continua vendo ela (RN-CE58).
            if (variantId is not null && product?.Variants.Any(x => x.Id == variantId && x.RemovedOn == null) != true)
                failures.Add(new($"Variants[{i}].Id", "Esta variação não pertence ao produto."));
        }

        return failures;
    }

    /// <summary>
    /// Variação que sai do formulário não pode ter saldo diferente de zero (RN-CE51): o saldo ficaria preso numa variação
    /// que nenhuma tela mostra. Movimentação antiga não impede, porque a removida continua gravada (RN-CE58).
    /// </summary>
    public static async Task<List<ValidationFailure>> RemovedVariantsWithBalance(
        IStockBalanceRepository balances, ProductRequestDto dto, Product product, CancellationToken ct)
    {
        var kept = dto.Variants.Select(v => v.Id).ToHashSet();
        var removed = product.Variants.Where(v => v.RemovedOn == null && !kept.Contains(v.Id)).Select(v => v.Id).ToList();
        if (removed.Count == 0)
            return [];

        return await balances.Exists(b => removed.Contains(b.VariantId) && b.Quantity != 0, ct)
            ? [new(nameof(dto.Variants), "Uma variação removida tem saldo de estoque. Zere ou transfira o saldo antes de remover.")]
            : [];
    }

    /// <summary>Evento existe e está ativo; o já vinculado pode estar inativo, para o produto seguir editável (RN-CE61).</summary>
    public static async Task<List<ValidationFailure>> Events(
        ICalendarEventRepository events, ProductRequestDto dto, ICollection<Guid> linked, CancellationToken ct)
    {
        var ids = dto.EventIds.Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var found = (await events.GetAll(e => ids.Contains(e.Id), 0, int.MaxValue, ct: ct)).ToList();
        var valid = ids.All(id => found.Any(e => e.Id == id && (e.IsActive || linked.Contains(id))));

        return valid ? [] : [new(nameof(dto.EventIds), "Escolha eventos ativos.")];
    }

    /// <summary>Fornecedor existe e está ativo; o já vinculado pode estar inativo, com o vínculo intacto (RN-CE60).</summary>
    public static async Task<List<ValidationFailure>> Suppliers(
        ISupplierRepository suppliers, ProductRequestDto dto, ICollection<Guid> linked, CancellationToken ct)
    {
        var ids = dto.Suppliers.Select(s => s.SupplierId).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var found = (await suppliers.GetAll(s => ids.Contains(s.Id), 0, int.MaxValue, ct: ct)).ToList();
        var failures = new List<ValidationFailure>();
        for (var i = 0; i < dto.Suppliers.Count; i++)
        {
            var id = dto.Suppliers[i].SupplierId;
            if (!found.Any(s => s.Id == id && (s.IsActive || linked.Contains(id))))
                failures.Add(new($"Suppliers[{i}].SupplierId", "Escolha um fornecedor ativo."));
        }

        return failures;
    }

    /// <summary>Local do mínimo existe e está aberto (ativo, de loja ativa); o que já tem mínimo pode estar fechado (RN-CE33).</summary>
    public static async Task<List<ValidationFailure>> Locations(
        IStockLocationRepository locations, ProductRequestDto dto, ICollection<Guid> linked, CancellationToken ct)
    {
        var ids = dto.LocationMinimums.Select(m => m.LocationId).Distinct().ToList();
        if (ids.Count == 0)
            return [];

        var open = (await locations.GetAll(l => ids.Contains(l.Id) && l.IsActive && l.Store.IsActive, 0, int.MaxValue, ct: ct))
            .Select(l => l.Id).ToHashSet();
        var failures = new List<ValidationFailure>();
        for (var i = 0; i < dto.LocationMinimums.Count; i++)
        {
            var id = dto.LocationMinimums[i].LocationId;
            if (!open.Contains(id) && !linked.Contains(id))
                failures.Add(new($"LocationMinimums[{i}].LocationId", "Escolha um local ativo de uma loja ativa."));
        }

        return failures;
    }

    /// <summary>
    /// SKU e código de barras únicos entre todos os produtos, ativos ou não, sem diferenciar maiúsculas (T2). Vale também para
    /// a variação removida, que continua gravada com o código, porque o histórico aponta para ela (RN-CE57, RN-CE58).
    /// </summary>
    public static async Task<List<ValidationFailure>> Codes(
        IProductVariantRepository variants, ProductRequestDto dto, CancellationToken ct)
    {
        var skus = dto.Variants.Select(v => v.Sku.Trim().ToLower()).ToList();
        var barcodes = dto.Variants
            .Where(v => !string.IsNullOrWhiteSpace(v.Barcode))
            .Select(v => v.Barcode!.Trim().ToLower())
            .ToList();

        var taken = (await variants.GetAll(
            v => skus.Contains(v.Sku.ToLower()) || (v.Barcode != null && barcodes.Contains(v.Barcode.ToLower())),
            0, int.MaxValue, ct: ct)).ToList();

        var failures = new List<ValidationFailure>();
        for (var i = 0; i < dto.Variants.Count; i++)
        {
            var variant = dto.Variants[i];
            var sku = variant.Sku.Trim();
            var barcode = variant.Barcode?.Trim();

            var skuOwner = taken.FirstOrDefault(o => o.Id != variant.Id && string.Equals(o.Sku, sku, StringComparison.OrdinalIgnoreCase));
            if (skuOwner is not null)
                failures.Add(new($"Variants[{i}].Sku", skuOwner.RemovedOn is null ? "Este SKU já está em uso." : "Este SKU é de uma variação removida e segue reservado pelo histórico."));

            var barcodeOwner = string.IsNullOrEmpty(barcode)
                ? null
                : taken.FirstOrDefault(o => o.Id != variant.Id && string.Equals(o.Barcode, barcode, StringComparison.OrdinalIgnoreCase));
            if (barcodeOwner is not null)
                failures.Add(new($"Variants[{i}].Barcode", barcodeOwner.RemovedOn is null ? "Este código de barras já está em uso." : "Este código de barras é de uma variação removida e segue reservado pelo histórico."));
        }

        return failures;
    }
}

public class CreateProductUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    IProductVariantRepository variantRepository,
    ICalendarEventRepository calendarEventRepository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ProductRequestDto> validator,
    IResultFactory<ProductResponseDto> resultFactory)
    : CreateUseCaseBase<Product, IProductRepository, ProductRequestDto, ProductResponseDto>(
        productRepository, unitOfWork, validator, resultFactory)
{
    private Product? _saved;

    protected override async Task<ResultDto<ProductResponseDto>?> ValidateBusinessRules(ProductRequestDto dto, CancellationToken ct)
    {
        if (UserId is null)
            return _resultFactory.Failure(ProductRules.UserRequiredMessage, 401);

        var failures = ProductRules.Ownership(dto, null);
        failures.AddRange(await ProductRules.Categories(categoryRepository, dto, [], ct));
        failures.AddRange(await ProductRules.Collection(collectionRepository, dto, null, ct));
        failures.AddRange(await ProductRules.Codes(variantRepository, dto, ct));
        failures.AddRange(await ProductRules.Events(calendarEventRepository, dto, [], ct));
        failures.AddRange(await ProductRules.Suppliers(supplierRepository, dto, [], ct));
        failures.AddRange(await ProductRules.Locations(locationRepository, dto, [], ct));

        return failures.Count == 0 ? null : _resultFactory.Failure(new ValidationResult(failures));
    }

    protected override Product MapToEntity(ProductRequestDto dto) => new(dto, UserId.GetValueOrDefault(), clock.Today);

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
            .Include(p => p.Variants).ThenInclude(v => v.Balances)
            .Include(p => p.Events).ThenInclude(e => e.Event);
}

/// <summary>Trocar o tipo não apaga nada: ele só define padrões e campos da tela (RN-CE20, RN-CE55). Inativo não edita (RN-CE04, na base).</summary>
public class UpdateProductUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    IProductVariantRepository variantRepository,
    ICalendarEventRepository calendarEventRepository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<ProductRequestDto> validator,
    IResultFactory<ProductResponseDto> resultFactory)
    : UpdateUseCaseBase<Product, IProductRepository, ProductRequestDto, ProductResponseDto>(
        productRepository, unitOfWork, validator, resultFactory)
{
    private Product? _saved;

    protected override async Task<ResultDto<ProductResponseDto>?> ValidateBusinessRules(Product entity, ProductRequestDto dto, CancellationToken ct)
    {
        if (UserId is null)
            return _resultFactory.Failure(ProductRules.UserRequiredMessage, 401);

        var failures = ProductRules.Ownership(dto, entity);
        failures.AddRange(await ProductRules.Categories(categoryRepository, dto, [.. entity.Categories.Select(c => c.CategoryId)], ct));
        failures.AddRange(await ProductRules.Collection(collectionRepository, dto, entity.CollectionId, ct));
        failures.AddRange(await ProductRules.RemovedVariantsWithBalance(balanceRepository, dto, entity, ct));
        failures.AddRange(await ProductRules.Codes(variantRepository, dto, ct));
        failures.AddRange(await ProductRules.Events(calendarEventRepository, dto, [.. entity.Events.Select(e => e.EventId)], ct));
        failures.AddRange(await ProductRules.Suppliers(supplierRepository, dto, [.. entity.Suppliers.Select(s => s.SupplierId)], ct));
        failures.AddRange(await ProductRules.Locations(
            locationRepository, dto, [.. entity.Variants.SelectMany(v => v.LocationMinimums).Select(m => m.LocationId)], ct));

        return failures.Count == 0 ? null : _resultFactory.Failure(new ValidationResult(failures));
    }

    protected override void UpdateEntity(Product entity, ProductRequestDto request)
        => entity.Update(request, UserId.GetValueOrDefault(), clock.Today);

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
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            // Só a faixa aberta: é a única que uma troca de preço toca (RN-CE59).
            .Include(p => p.Variants).ThenInclude(v => v.PriceHistory.Where(entry => entry.ValidTo == null))
            .Include(p => p.Variants).ThenInclude(v => v.Balances)
            .Include(p => p.Variants).ThenInclude(v => v.LocationMinimums)
            .Include(p => p.Events).ThenInclude(e => e.Event)
            .Include(p => p.Suppliers);
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
            .Include(p => p.Variants).ThenInclude(v => v.Balances)
            .Include(p => p.Events).ThenInclude(e => e.Event);
}

/// <summary>
/// Aba Variações do T3: as variações do produto, na ordem da grade, com custo médio, último custo, margem e disponível da
/// empresa (RN-CE31, RN-CE08). A removida só aparece na aba Preços (RN-CE58).
/// </summary>
public class GetProductVariantsUseCase(
    IProductRepository productRepository,
    IProductPricingService pricing,
    IResultFactory<List<ProductVariantResponseDto>> resultFactory)
    : GetByIdUseCaseBase<Product, IProductRepository, List<ProductVariantResponseDto>>(productRepository, resultFactory)
{
    protected override List<ProductVariantResponseDto> MapToResponseDto(Product entity)
        => [.. entity.Variants.Where(v => v.RemovedOn == null).OrderBy(v => v.SortOrder).Select(v => new ProductVariantResponseDto(v, pricing))];

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Variants).ThenInclude(v => v.Balances)
            .Include(p => p.Variants)
            .ThenInclude(v => v.AttributeValues)
            .ThenInclude(link => link.AttributeValue)
            .ThenInclude(value => value.Attribute);
}

/// <summary>Aba Fornecedores do T3: o preferido primeiro, depois pelo nome.</summary>
public class GetProductSuppliersUseCase(
    IProductRepository productRepository,
    IResultFactory<List<ProductSupplierResponseDto>> resultFactory)
    : GetByIdUseCaseBase<Product, IProductRepository, List<ProductSupplierResponseDto>>(productRepository, resultFactory)
{
    protected override List<ProductSupplierResponseDto> MapToResponseDto(Product entity)
        => [.. entity.Suppliers.Select(link => new ProductSupplierResponseDto(link)).OrderByDescending(s => s.IsPreferred).ThenBy(s => s.SupplierName)];

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Suppliers).ThenInclude(link => link.Supplier);
}

/// <summary>
/// Aba Preços do T3: o mais recente primeiro. No mesmo dia, desempata pela hora da gravação, porque cada troca é uma linha
/// (RN-CE59). Inclui as variações removidas: o histórico delas continua valendo (RN-CE58).
/// </summary>
public class GetProductPriceHistoryUseCase(
    IProductRepository productRepository,
    IResultFactory<List<PriceHistoryResponseDto>> resultFactory)
    : GetByIdUseCaseBase<Product, IProductRepository, List<PriceHistoryResponseDto>>(productRepository, resultFactory)
{
    protected override List<PriceHistoryResponseDto> MapToResponseDto(Product entity)
        => [.. entity.Variants
            .SelectMany(variant => variant.PriceHistory.Select(entry => (entry, variant)))
            .OrderByDescending(row => row.entry.ValidFrom)
            .ThenByDescending(row => row.entry.CreatedAt)
            .Select(row => new PriceHistoryResponseDto(row.entry, row.variant))];

    protected override Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Variants).ThenInclude(v => v.PriceHistory).ThenInclude(entry => entry.ChangedBy)
            .Include(p => p.Variants)
            .ThenInclude(v => v.AttributeValues)
            .ThenInclude(link => link.AttributeValue)
            .ThenInclude(value => value.Attribute);
}

/// <summary>
/// Listagem (T1). Filtros: search, categoryId (nível 1 traz as filhas), productType, collectionId, lifecycle, isActive e
/// storeId, que não filtra produto: só escolhe a loja do disponível e dos mínimos (sem ele, a empresa inteira).
/// F1.8: lowStock = "só estoque baixo" (RN-CE33) e ordenação por disponível.
/// </summary>
public class GetAllProductsUseCase(
    IProductRepository productRepository,
    IStockBalanceRepository balanceRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IResultFactory<PaginationDto<ProductListItemDto>> resultFactory)
    : GetAllUseCaseBase<Product, IProductRepository, ProductListItemDto>(productRepository, resultFactory)
{
    private Guid? _storeId;
    private HashSet<Guid>? _lowStockFilter;
    private Dictionary<Guid, decimal> _available = new();
    private HashSet<Guid> _lowStock = [];

    /// <summary>
    /// "Só estoque baixo" depende de uma consulta de variação × local que não cabe no predicado do produto: resolvida antes,
    /// vira um filtro por id, para a paginação e o total continuarem no banco.
    /// </summary>
    public override async Task<ResultDto<PaginationDto<ProductListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        var set = new FilterSet(filters);
        _storeId = set.TryGetString("storeId", out var store) && Guid.TryParse(store, out var storeId) ? storeId : null;
        _lowStockFilter = set.TryGetString("lowStock", out var raw) && FilterValueParsers.TryParseBool(raw, out var lowStock) && lowStock
            ? await balanceRepository.GetLowStockProductIds(null, _storeId, ct)
            : null;

        return await base.Execute(offset, limit, filters, sort, order, ct);
    }

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
                || p.Variants.Any(v => v.RemovedOn == null && (v.Sku.ToLower().Contains(term)
                    || (v.Barcode != null && v.Barcode.ToLower().Contains(term)))));
        }

        if (set.TryGetString("categoryId", out var raw) && Guid.TryParse(raw, out var categoryId))
            builder.And(p => p.Categories.Any(c => c.CategoryId == categoryId || c.Category.ParentId == categoryId));

        if (_lowStockFilter is { } lowStockIds)
            builder.And(p => lowStockIds.Contains(p.Id));

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
            // Mesmo recorte do disponível da linha: locais ativos de loja ativa, da loja do filtro ou da empresa (RN-CE08).
            ["available"] = AvailableSort(_storeId),
        };

    private static Expression<Func<Product, object?>> AvailableSort(Guid? storeId)
        => p => p.Variants.SelectMany(v => v.Balances)
            .Where(b => b.Location.IsActive && b.Location.Store.IsActive && (storeId == null || b.Location.StoreId == storeId))
            .Sum(b => (decimal?)b.Quantity) ?? 0m;

    /// <summary>Disponível e estoque baixo da página, numa consulta cada, calculados no banco (RN-CE08, RN-CE33).</summary>
    protected override async Task PrepareMapping(IReadOnlyList<Product> items, CancellationToken ct)
    {
        var ids = items.Select(p => p.Id).ToList();
        _available = await balanceRepository.SumByProduct(ids, _storeId, ct);
        _lowStock = await balanceRepository.GetLowStockProductIds(ids, _storeId, ct);
    }

    protected override ProductListItemDto MapToResponseDto(Product entity)
        => new(entity, fileStorage.GetPublicUrl(entity.ImageKey), pricing, _available.GetValueOrDefault(entity.Id), _lowStock.Contains(entity.Id));
}

/// <summary>
/// Aba Estoque do T3 (contrato, linha da matriz): uma linha por variação ativa, uma coluna por local ativo de loja ativa
/// (da loja informada ou de todas). Célula sem saldo gravado vale zero.
/// </summary>
public class GetProductStockUseCase(
    IProductRepository productRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IResultFactory<StockMatrixDto> resultFactory) : IGetProductStockUseCase
{
    public async Task<ResultDto<StockMatrixDto>> Execute(Guid id, Guid? storeId, CancellationToken ct = default)
    {
        var product = await productRepository.GetById(id, GetInclude(), ct);
        if (product is null)
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        var locations = (await locationRepository.GetAll(
            l => l.IsActive && l.Store.IsActive && (storeId == null || l.StoreId == storeId),
            0,
            int.MaxValue,
            GetLocationInclude(),
            q => q.OrderBy(l => l.Store.Name).ThenBy(l => l.Name).ThenBy(l => l.Id),
            ct)).ToList();

        var variants = product.Variants.Where(v => v.RemovedOn == null && v.IsActive).OrderBy(v => v.SortOrder).ToList();
        var variantIds = variants.Select(v => v.Id).ToList();
        var locationIds = locations.Select(l => l.Id).ToList();
        var balances = (await balanceRepository.GetAll(
            b => variantIds.Contains(b.VariantId) && locationIds.Contains(b.LocationId), 0, int.MaxValue, ct: ct)).ToList();

        return resultFactory.Success(new StockMatrixDto
        {
            Locations = [.. locations.Select(l => new StockMatrixLocationDto(l))],
            Rows = [.. variants.Select(variant => new StockMatrixRowDto
            {
                VariantId = variant.Id,
                VariantLabel = ProductVariantResponseDto.LabelOf(variant),
                Sku = variant.Sku,
                Cells = [.. locations.Select(location => new StockMatrixCellDto(
                    location.Id,
                    balances.Where(b => b.VariantId == variant.Id && b.LocationId == location.Id).Sum(b => b.Quantity),
                    product.EffectiveMinimum(variant, location.Id)))],
            })],
        });
    }

    private static Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Variants).ThenInclude(v => v.LocationMinimums)
            .Include(p => p.Variants)
            .ThenInclude(v => v.AttributeValues)
            .ThenInclude(link => link.AttributeValue)
            .ThenInclude(value => value.Attribute);

    private static Func<IQueryable<StockLocation>, IQueryable<StockLocation>> GetLocationInclude() => q => q.Include(l => l.Store);
}

/// <summary>Monta o formulário T2. As opções trazem as ativas e as já vinculadas, para o combo mostrar o que está gravado.</summary>
public class GetProductFormUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    ICollectionRepository collectionRepository,
    ICalendarEventRepository calendarEventRepository,
    ISupplierRepository supplierRepository,
    IStockLocationRepository locationRepository,
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
        var linkedEvents = product?.Events.Select(e => e.EventId).ToList() ?? [];
        var linkedSuppliers = product?.Suppliers.Select(s => s.SupplierId).ToList() ?? [];

        var categories = await categoryRepository.GetFormOptions(linkedCategories, ct);
        var collections = await collectionRepository.GetAll(
            c => c.IsActive || c.Id == linkedCollection,
            0,
            int.MaxValue,
            orderBy: q => q.OrderBy(c => c.Name).ThenBy(c => c.Id),
            ct: ct);
        var events = await calendarEventRepository.GetAll(
            e => e.IsActive || linkedEvents.Contains(e.Id),
            0,
            int.MaxValue,
            orderBy: q => q.OrderBy(e => e.Name).ThenBy(e => e.Id),
            ct: ct);
        var suppliers = await supplierRepository.GetAll(
            s => s.IsActive || linkedSuppliers.Contains(s.Id),
            0,
            int.MaxValue,
            orderBy: q => q.OrderBy(s => s.Name).ThenBy(s => s.Id),
            ct: ct);
        var linkedLocations = product?.Variants.SelectMany(v => v.LocationMinimums).Select(m => m.LocationId).Distinct().ToList() ?? [];
        var locations = await locationRepository.GetAll(
            l => (l.IsActive && l.Store.IsActive) || linkedLocations.Contains(l.Id),
            0,
            int.MaxValue,
            q => q.Include(l => l.Store),
            q => q.OrderBy(l => l.Store.Name).ThenBy(l => l.Name).ThenBy(l => l.Id),
            ct);

        var options = new ProductFormOptionsDto
        {
            Categories = [.. categories.Select(c => new CategoryOptionDto(c))],
            Collections = [.. collections.Select(c => new Option(c.Id.ToString(), c.Name))],
            Events = [.. events.Select(e => new Option(e.Id.ToString(), e.Name))],
            Suppliers = [.. suppliers.Select(s => new SupplierOptionDto(s))],
            Locations = [.. locations.Select(l => new LocationOptionDto(l))],
        };

        return resultFactory.Success(product is null
            ? new ProductFormDto(options)
            : new ProductFormDto(product, fileStorage.GetPublicUrl(product.ImageKey), options));
    }

    private static Func<IQueryable<Product>, IQueryable<Product>> GetInclude()
        => q => q.Include(p => p.Categories)
            .Include(p => p.Attributes).ThenInclude(a => a.Values)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            .Include(p => p.Variants).ThenInclude(v => v.LocationMinimums)
            .Include(p => p.Events)
            .Include(p => p.Suppliers);
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
