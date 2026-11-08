using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Casos de teste do seed demo (só com o histórico ligado, <c>Seed:Demo:HistoryMonths</c> &gt; 0), datados a partir de hoje, para toda tela ter o que mostrar:
/// promoções em cada status (vigente no produto e numa variação, agendada, encerrada, cancelada) e pedidos de compra fora do
/// fluxo do histórico (orçamento, pendente atrasado, cancelado e um recebido ontem com lotes vencendo em até 30 dias) e uma
/// transferência errada do Shopping para o Centro, que a análise transforma em "Transferir" (F3.4b).
/// <para>
/// Roda depois do histórico (60) e antes dos dados derivados (70). Cada grupo é idempotente pela marca <see cref="Tag"/> na
/// nota ou no motivo; o recebimento passa pelo <see cref="IStockLedger"/> como a F2.8.
/// </para>
/// </summary>
public class DemoScenariosSeeder(
    IConfiguration configuration,
    AppDbContext context,
    IUserRepository userRepository,
    IStoreRepository storeRepository,
    ISupplierRepository supplierRepository,
    IProductVariantRepository variantRepository,
    IPromotionRepository promotionRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    ILotRepository lotRepository,
    IStockLedger stockLedger,
    IStockTransferService transferService,
    IStockBalanceRepository balanceRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork) : ISeeder
{
    /// <summary>Marca dos registros deste seeder, no fim do motivo da promoção e da nota do pedido de compra.</summary>
    public const string Tag = "(cenário demo)";

    /// <summary>Validade dos lotes do recebimento de ontem: dentro da janela de 30 dias do alerta de vencimento.</summary>
    public static readonly int[] ShortLotDays = [12, 25];

    /// <summary>
    /// Alimentos sem data comemorativa cujo estoque do Shopping vai quase todo para o Centro ("transferência errada"): o Shopping
    /// fica abaixo do mínimo e o Centro com excesso, então a análise recomenda "Transferir" (RN-DF16, F3.4b).
    /// </summary>
    public static readonly string[] TransferImbalanceSkus = ["DP-025", "DP-026", "DP-027", "DP-011"];

    /// <summary>O que fica no salão do Shopping depois da transferência errada.</summary>
    public const decimal TransferLeftBehind = 2m;

    private readonly HashSet<BaseModel> _stamped = [];

    public int Order => 65;

    public async Task SeedAsync(CancellationToken ct)
    {
        // Só com o histórico (dev e staging): o catálogo puro da F1.11 fica sem casos extras.
        if (DemoHistorySeeder.HistoryMonths(configuration) == 0)
            return;

        var admin = await userRepository.Get(u => u.Email == UsersSeeder.AdminEmail, ct: ct);
        var stores = (await storeRepository.GetAll(s => DemoCatalog.StoreNames.Contains(s.Name), 0, int.MaxValue, ct: ct))
            .OrderBy(s => Array.IndexOf(DemoCatalog.StoreNames, s.Name))
            .ToList();
        if (admin is null || stores.Count < DemoCatalog.StoreNames.Length)
            return;

        var skus = DemoCatalog.Products.SelectMany(DemoCatalog.VariantsOf).Select(v => v.Sku).ToList();
        var variants = (await variantRepository.GetAllForUpdate(v => skus.Contains(v.Sku), ct)).ToDictionary(v => v.Sku);
        if (variants.Count == 0)
            return;

        if (!await promotionRepository.Exists(p => p.Reason != null && p.Reason.EndsWith(Tag), ct))
            await SeedPromotions(admin.Id, variants, ct);

        if (!await purchaseOrderRepository.Exists(p => p.Note != null && p.Note.EndsWith(Tag), ct))
            await SeedPurchaseOrders(admin.Id, stores, variants, ct);

        if (!await context.Set<StockTransfer>().AnyAsync(t => t.Note != null && t.Note.EndsWith(Tag), ct))
            await SeedTransferImbalance(admin.Id, stores, variants, ct);
    }

    /// <summary>Transferência errada do Shopping para o Centro (salão para salão), deixando <see cref="TransferLeftBehind"/> no Shopping.</summary>
    private async Task SeedTransferImbalance(Guid adminId, List<Store> stores, Dictionary<string, ProductVariant> variants, CancellationToken ct)
    {
        if (stores[0].DefaultSaleLocationId is not { } centro || stores[1].DefaultSaleLocationId is not { } shopping)
            return;

        var variantIds = TransferImbalanceSkus.Where(variants.ContainsKey).Select(sku => variants[sku].Id).ToList();
        var available = (await balanceRepository.GetLocationPositions(ct))
            .Where(p => p.LocationId == shopping && variantIds.Contains(p.VariantId))
            .ToDictionary(p => p.VariantId, p => p.Available);
        var items = variantIds
            .Where(id => available.GetValueOrDefault(id) > TransferLeftBehind)
            .Select(id => new StockTransferItemRequestDto { VariantId = id, Quantity = available[id] - TransferLeftBehind })
            .ToList();
        if (items.Count == 0)
            return;

        var (transfer, failures) = await transferService.Transfer(new StockTransferRequestDto
        {
            FromLocationId = shopping,
            ToLocationId = centro,
            Note = $"Mandado para o Centro por engano {Tag}",
            Items = items,
        }, adminId, null, ct);
        if (transfer is null)
            throw new InvalidOperationException($"Cenário demo de transferência recusado: {string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"))}");

        Stamp(clock.StartOfDayUtc(clock.Today.AddDays(-2)).AddHours(16));
        await unitOfWork.CommitAsync(ct);
    }

    private async Task SeedPromotions(Guid adminId, Dictionary<string, ProductVariant> variants, CancellationToken ct)
    {
        var today = clock.Today;
        await AddPromotion(adminId, variants["DP-030"], wholeProduct: true, 15, today.AddDays(-5), today.AddDays(9), "Queima de estoque", ct);
        await AddPromotion(adminId, variants["DP-017-PIS"], wholeProduct: false, 10, today.AddDays(-2), today.AddDays(12), "Trufa de pistache da semana", ct);
        await AddPromotion(adminId, variants["DP-021"], wholeProduct: true, 10, today.AddDays(20), today.AddDays(45), "Esquenta de Natal", ct);
        await AddPromotion(adminId, variants["DP-039"], wholeProduct: true, 20, today.AddDays(-60), today.AddDays(-46), "Volta às aulas", ct);
        var canceled = await AddPromotion(adminId, variants["DP-042"], wholeProduct: true, 12, today.AddDays(3), today.AddDays(10),
            "Semana do alfajor", ct);
        canceled.Cancel();

        await unitOfWork.CommitAsync(ct);
    }

    private async Task<Promotion> AddPromotion(Guid adminId, ProductVariant variant, bool wholeProduct, decimal discountPercent,
        DateOnly startDate, DateOnly endDate, string reason, CancellationToken ct)
    {
        var promotion = new Promotion(new PromotionRequestDto
        {
            ProductId = variant.ProductId,
            VariantId = wholeProduct ? null : variant.Id,
            DiscountPercent = discountPercent,
            StartDate = startDate,
            EndDate = endDate,
            Reason = $"{reason} {Tag}",
        }, adminId, belowCostConfirmed: false);
        await promotionRepository.AddAsync(promotion, ct);
        return promotion;
    }

    private async Task SeedPurchaseOrders(Guid adminId, List<Store> stores, Dictionary<string, ProductVariant> variants,
        CancellationToken ct)
    {
        var today = clock.Today;
        var suppliers = await SupplierIds(ct);
        var center = stores[0];
        var mall = stores[1];

        // Orçamento: ainda não enviado, então não conta como "a caminho" nem atrasa.
        await AddPurchaseOrder(adminId, center, suppliers[0], "Orçamento para conferir com o fornecedor",
            [("DP-030", 24m), ("DP-039", 48m)], variants, createdOn: today.AddDays(-1), ct);
        Stamp(clock.StartOfDayUtc(today.AddDays(-1)).AddHours(15));

        // Pendente atrasado: enviado há 30 dias com prazo de 25 (Casa Bellucci), previsão vencida há 5 dias.
        var late = await AddPurchaseOrder(adminId, center, suppliers[2], "Pedido atrasado pelo fornecedor",
            [("DP-040", 24m), ("DP-041", 12m)], variants, createdOn: today.AddDays(-30), ct);
        late.Send(clock.StartOfDayUtc(today.AddDays(-30)).AddHours(9), today.AddDays(-30));
        Stamp(clock.StartOfDayUtc(today.AddDays(-30)).AddHours(9));

        // Cancelado antes do envio.
        var canceled = await AddPurchaseOrder(adminId, mall, suppliers[3], "Fornecedor sem estoque",
            [("DP-035", 20m), ("DP-036", 30m)], variants, createdOn: today.AddDays(-12), ct);
        canceled.Cancel("Fornecedor avisou que está sem estoque", clock.StartOfDayUtc(today.AddDays(-10)).AddHours(11));
        Stamp(clock.StartOfDayUtc(today.AddDays(-12)).AddHours(14));

        await unitOfWork.CommitAsync(ct);

        await ReceiveShortDated(adminId, mall, suppliers[1], variants, ct);
    }

    /// <summary>
    /// Recebido ontem na loja do shopping, com lotes de validade curta (<see cref="ShortLotDays"/>): alimenta o selo "vence em até
    /// 30 dias" e o aviso de lote vencendo. Entrada In/Purchase pelo livro-razão, como a F2.8.
    /// </summary>
    private async Task ReceiveShortDated(Guid adminId, Store store, Guid supplierId, Dictionary<string, ProductVariant> variants,
        CancellationToken ct)
    {
        var today = clock.Today;
        var sentOn = today.AddDays(-16);
        var receivedOn = today.AddDays(-1);
        var order = await AddPurchaseOrder(adminId, store, supplierId, "Lote com validade curta",
            [("DP-001", 30m), ("DP-002", 18m)], variants, createdOn: sentOn, ct);
        var sentAt = clock.StartOfDayUtc(sentOn).AddHours(9);
        order.Send(sentAt, sentOn);
        Stamp(sentAt);

        var lines = new List<StockLineDto>();
        var items = order.Items.OrderBy(i => i.SortOrder).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var lot = new Lot(items[i].VariantId, $"VC{receivedOn:yyMMdd}{i + 1}", today.AddDays(ShortLotDays[i % ShortLotDays.Length]));
            await lotRepository.AddAsync(lot, ct);
            lines.Add(new StockLineDto
            {
                VariantId = items[i].VariantId,
                LocationId = order.ReceiveLocationId,
                LotId = lot.Id,
                Type = MovementTypeEnum.In,
                Reason = MovementReasonEnum.Purchase,
                Quantity = items[i].Quantity,
                UnitCost = items[i].UnitCost,
            });
        }

        var failures = await stockLedger.Post(
            new StockDocumentDto { SourceType = SourceTypeEnum.PurchaseOrder, SourceId = order.Id, UserId = adminId }, lines, ct);
        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Cenário demo recusado pelo livro-razão: {string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"))}");

        var byId = variants.Values.ToDictionary(v => v.Id);
        foreach (var item in order.Items)
            item.RecordReceipt(byId[item.VariantId].AverageCost);

        var receivedAt = clock.StartOfDayUtc(receivedOn).AddHours(8);
        order.Receive(adminId, receivedAt, receivedOn, sentOn);
        Stamp(receivedAt);
        await unitOfWork.CommitAsync(ct);
    }

    private async Task<PurchaseOrder> AddPurchaseOrder(Guid adminId, Store store, Guid supplierId, string note,
        (string Sku, decimal Quantity)[] items, Dictionary<string, ProductVariant> variants, DateOnly createdOn, CancellationToken ct)
    {
        var supplier = DemoCatalog.Suppliers[DemoCatalog.Products.First(p => items[0].Sku.StartsWith(p.Code)).SupplierIndex];
        var dto = new SavePurchaseOrderDto
        {
            SupplierId = supplierId,
            ReceiveLocationId = store.DefaultSaleLocationId!.Value,
            Note = $"{note} {Tag}",
            Items = items.Select(i => new SavePurchaseItemDto
            {
                VariantId = variants[i.Sku].Id,
                Quantity = i.Quantity,
                UnitCost = DemoCatalog.Products.SelectMany(DemoCatalog.VariantsOf).First(v => v.Sku == i.Sku).Cost,
            }).ToList(),
        };

        var order = new PurchaseOrder(dto, store.Id, adminId, supplier.LeadTimeDays, createdOn);
        await purchaseOrderRepository.AddAsync(order, ct);
        return order;
    }

    private async Task<List<Guid>> SupplierIds(CancellationToken ct)
    {
        var documents = DemoCatalog.Suppliers.Select(s => TaxDocument.ParseCnpj(s.Cnpj)).ToList();
        var byCnpj = (await supplierRepository.GetAll(s => documents.Contains(s.Cnpj), 0, int.MaxValue, ct: ct))
            .ToDictionary(s => s.Cnpj.Value, s => s.Id);
        return documents.Select(d => byCnpj[d.Value]).ToList();
    }

    /// <summary>Data do cenário no <c>CreatedAt</c> do que entrou no rastreador desde o último ajuste (como o histórico).</summary>
    private void Stamp(DateTime at)
    {
        foreach (var entry in context.ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added && _stamped.Add(entry.Entity))
                entry.Property(nameof(BaseModel.CreatedAt)).CurrentValue = at;
        }
    }
}
