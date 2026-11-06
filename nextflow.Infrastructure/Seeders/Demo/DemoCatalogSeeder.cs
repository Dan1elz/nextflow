using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Catálogo de demonstração (F1.11), só com <c>Seed:Demo:Enabled</c>: duas lojas com "Loja" e "Depósito", fornecedores,
/// categorias, ~40 produtos de doces e presentes (com e sem grade), vínculos com fornecedor e evento, mínimos por local e
/// saldo de abertura com custo.
/// <para>
/// Duas etapas, cada uma idempotente e num commit só. O catálogo roda se a categoria raiz não existe. O saldo de abertura
/// roda se nenhum local das lojas demo tem movimento, e passa pelo <see cref="IStockLedger"/> (RN-CE01, RN-CE14): o
/// livro-razão lê as variações do banco, por isso o catálogo precisa estar gravado antes.
/// </para>
/// </summary>
public class DemoCatalogSeeder(
    IConfiguration configuration,
    IStoreRepository storeRepository,
    ICategoryRepository categoryRepository,
    ISupplierRepository supplierRepository,
    ICalendarEventRepository calendarEventRepository,
    IProductRepository productRepository,
    IProductVariantRepository variantRepository,
    IStockMovementRepository movementRepository,
    IUserRepository userRepository,
    IStoreMembership storeMembership,
    IStockLedger stockLedger,
    IBusinessClock clock,
    IUnitOfWork unitOfWork) : ISeeder
{
    public const string EnabledKey = "Seed:Demo:Enabled";
    public const string OpeningNote = "Saldo de abertura (demonstração)";

    public int Order => 50;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!bool.TryParse(configuration[EnabledKey], out var enabled) || !enabled)
            return;

        var user = await userRepository.Get(u => u.Email == UsersSeeder.AdminEmail, ct: ct)
            ?? throw new InvalidOperationException("O seed demo precisa do Admin criado pelo UsersSeeder.");

        if (!await categoryRepository.Exists(c => c.Name == DemoCatalog.RootCategory, ct))
            await SeedCatalog(user.Id, ct);

        await SeedOpeningBalances(user.Id, ct);
    }

    private async Task SeedCatalog(Guid userId, CancellationToken ct)
    {
        var stores = new List<Store>();
        foreach (var name in DemoCatalog.StoreNames)
            stores.Add(await AddStore(name, ct));

        var root = new Category(new CategoryRequestDto { Name = DemoCatalog.RootCategory });
        await categoryRepository.AddAsync(root, ct);
        var categories = new Dictionary<string, Guid>();
        foreach (var name in DemoCatalog.Categories)
        {
            var category = new Category(new CategoryRequestDto { Name = name, ParentId = root.Id });
            await categoryRepository.AddAsync(category, ct);
            categories[name] = category.Id;
        }

        var suppliers = DemoCatalog.Suppliers
            .Select(s => new Supplier(new SupplierRequestDto { Name = s.Name, Cnpj = s.Cnpj, LeadTimeDays = s.LeadTimeDays }))
            .ToList();
        await supplierRepository.AddRangeAsync(suppliers, ct);

        var eventNames = DemoCatalog.Products.SelectMany(p => p.Events).Distinct().ToList();
        var events = (await calendarEventRepository.GetAll(e => eventNames.Contains(e.Name), 0, int.MaxValue, ct: ct))
            .ToDictionary(e => e.Name, e => e.Id);

        var today = clock.Today;
        foreach (var product in DemoCatalog.Products)
        {
            var dto = ToRequest(product, categories[product.Category], suppliers[product.SupplierIndex].Id,
                DemoCatalog.Suppliers[product.SupplierIndex], events, stores);
            await productRepository.AddAsync(new Product(dto, userId, today), ct);
        }

        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>Como o <c>CreateStoreUseCase</c>: local "Loja" vendável para venda, mais o "Depósito" para recebimento.</summary>
    private async Task<Store> AddStore(string name, CancellationToken ct)
    {
        var store = new Store(new StoreRequestDto { Name = name });
        var sale = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = StockLocation.DefaultName, IsSellable = true });
        var deposit = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = DemoCatalog.DepositName, IsSellable = false });
        store.Locations.Add(sale);
        store.Locations.Add(deposit);
        store.SetDefaults(sale.Id, deposit.Id);

        await storeRepository.AddAsync(store, ct);
        await storeMembership.LinkStore(store.Id, ct);
        return store;
    }

    private static ProductRequestDto ToRequest(DemoProduct product, Guid categoryId, Guid supplierId, DemoSupplier supplier,
        Dictionary<string, Guid> events, List<Store> stores)
    {
        var variants = DemoCatalog.VariantsOf(product);
        var dto = new ProductRequestDto
        {
            Name = product.Name,
            Brand = product.Brand,
            Description = product.Description,
            ProductType = product.IsFood ? ProductTypeEnum.Perishable : ProductTypeEnum.Standard,
            SaleUnit = SaleUnitEnum.Unit,
            DecimalPlaces = 0,
            CategoryIds = [categoryId],
            DefaultMinQuantity = product.MinStock,
            EventIds = product.Events.Where(events.ContainsKey).Select(name => events[name]).ToList(),
            Suppliers =
            [
                new ProductSupplierRequestDto
                {
                    SupplierId = supplierId,
                    IsPreferred = true,
                    SupplierSku = $"F{product.SupplierIndex + 1}-{product.Code}",
                    MinOrderQty = supplier.OrderMultiple > 1 ? supplier.OrderMultiple * 2 : null,
                    OrderMultiple = supplier.OrderMultiple > 1 ? supplier.OrderMultiple : null,
                },
            ],
        };

        if (product.Grid is { } grid)
        {
            dto.Attributes =
            [
                new ProductAttributeRequestDto
                {
                    Name = grid.Attribute,
                    Values = grid.Options.Select(o => new ProductAttributeValueRequestDto { TempKey = o.SkuSuffix, Value = o.Value }).ToList(),
                },
            ];
        }

        dto.Variants = variants.Select((v, index) => new ProductVariantRequestDto
        {
            Sku = v.Sku,
            Price = v.Price,
            AttributeValueIds = product.Grid is null ? [] : [product.Grid.Options[index].SkuSuffix],
        }).ToList();

        if (product.MinStock >= DemoCatalog.MainItemMinStock)
            dto.LocationMinimums = LocationMinimums(product.MinStock, variants.Count, stores);

        return dto;
    }

    /// <summary>
    /// Itens principais: a loja principal fica com o mínimo padrão no salão, a segunda com uma fração dele, e os depósitos
    /// guardam metade do mínimo do salão da própria loja (RN-CE33).
    /// </summary>
    private static List<ProductLocationMinimumDto> LocationMinimums(decimal minStock, int variantCount, List<Store> stores)
    {
        var minimums = new List<ProductLocationMinimumDto>();
        for (var storeIndex = 0; storeIndex < stores.Count; storeIndex++)
        {
            var saleMinimum = storeIndex == 0 ? minStock : Math.Ceiling(minStock * DemoCatalog.SecondStoreFactor);
            foreach (var location in stores[storeIndex].Locations)
            {
                var quantity = location.IsSellable ? saleMinimum : Math.Ceiling(saleMinimum / 2);
                for (var variantIndex = 0; variantIndex < variantCount; variantIndex++)
                    minimums.Add(new ProductLocationMinimumDto { VariantIndex = variantIndex, LocationId = location.Id, MinQuantity = quantity });
            }
        }
        return minimums;
    }

    private async Task SeedOpeningBalances(Guid userId, CancellationToken ct)
    {
        var stores = (await storeRepository.GetAll(s => DemoCatalog.StoreNames.Contains(s.Name), 0, int.MaxValue,
                q => q.Include(s => s.Locations), ct: ct))
            .OrderBy(s => Array.IndexOf(DemoCatalog.StoreNames, s.Name))
            .ToList();
        if (stores.Count == 0)
            return;

        var locationIds = stores.SelectMany(s => s.Locations).Select(l => l.Id).ToList();
        if (await movementRepository.Exists(m => locationIds.Contains(m.LocationId), ct))
            return;

        var skus = DemoCatalog.Products.SelectMany(DemoCatalog.VariantsOf).Select(v => v.Sku).ToList();
        var variantIds = (await variantRepository.GetAll(v => skus.Contains(v.Sku), 0, int.MaxValue, ct: ct))
            .ToDictionary(v => v.Sku, v => v.Id);

        var lines = new List<StockLineDto>();
        for (var storeIndex = 0; storeIndex < stores.Count; storeIndex++)
        {
            var factor = storeIndex == 0 ? 1m : DemoCatalog.SecondStoreFactor;
            foreach (var product in DemoCatalog.Products.Where(p => p.MinStock > 0))
            {
                foreach (var variant in DemoCatalog.VariantsOf(product).Where(v => variantIds.ContainsKey(v.Sku)))
                {
                    foreach (var location in stores[storeIndex].Locations)
                    {
                        // Salão com folga sobre o mínimo; depósito com o dobro do mínimo, de onde a loja repõe.
                        var quantity = Math.Ceiling(product.MinStock * factor * (location.IsSellable ? 1.5m : 2m));
                        lines.Add(new StockLineDto
                        {
                            VariantId = variantIds[variant.Sku],
                            LocationId = location.Id,
                            Type = MovementTypeEnum.In,
                            Reason = MovementReasonEnum.Opening,
                            Quantity = quantity,
                            UnitCost = variant.Cost,
                        });
                    }
                }
            }
        }

        if (lines.Count == 0)
            return;

        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = userId, Note = OpeningNote };
        var failures = await stockLedger.Post(document, lines, ct);
        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Saldo de abertura do seed demo recusado: {string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"))}");

        await unitOfWork.CommitAsync(ct);
    }
}
