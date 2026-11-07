using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;
using Nextflow.Infrastructure.Database;
using SaleOrder = Nextflow.Domain.Models.Order;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Histórico de demonstração (F2.9), só com <c>Seed:Demo:Enabled</c> e <c>Seed:Demo:HistoryMonths</c> &gt; 0: lança no
/// livro-razão, em ordem de data, o plano da <see cref="DemoHistorySimulation"/> (abertura no dia 0, vendas pagas de balcão e
/// pedidos de compra recebidos) com as datas no passado. Saldo = soma dos movimentos por construção (RN-CE01, RN-CE14).
/// <para>
/// Roda só se nenhum local das lojas demo tem movimento (a abertura da F1.11 não roda com histórico ligado). Commit por dia,
/// com o rastreador limpo depois, para a memória não crescer com 36 meses de registros. O <c>CreatedAt</c> dos registros
/// novos é ajustado no <see cref="AppDbContext"/> para a data simulada: só o seed faz isso.
/// </para>
/// </summary>
public class DemoHistorySeeder(
    IConfiguration configuration,
    AppDbContext context,
    IStoreRepository storeRepository,
    ISupplierRepository supplierRepository,
    IProductVariantRepository variantRepository,
    IStockMovementRepository movementRepository,
    IOrderRepository orderRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IUserRepository userRepository,
    IStockLedger stockLedger,
    ILotRepository lotRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    ILogger<DemoHistorySeeder> logger) : ISeeder
{
    public const string HistoryMonthsKey = "Seed:Demo:HistoryMonths";
    public const string AnchorDateKey = "Seed:Demo:AnchorDate";
    public const string OpeningNote = "Saldo de abertura do histórico (demonstração)";
    public const string PurchaseNote = "Reposição programada (dados de demonstração)";
    public const int RandomSeed = 42;

    private readonly HashSet<BaseModel> _stamped = [];
    private readonly Dictionary<(Guid VariantId, string Code), Guid> _lots = [];

    public int Order => 60;

    /// <summary>Meses de histórico configurados; 0 (ou flag do demo desligada) = sem histórico.</summary>
    public static int HistoryMonths(IConfiguration configuration)
        => bool.TryParse(configuration[DemoCatalogSeeder.EnabledKey], out var enabled) && enabled
           && int.TryParse(configuration[HistoryMonthsKey], out var months) && months > 0 ? months : 0;

    public async Task SeedAsync(CancellationToken ct)
    {
        var months = HistoryMonths(configuration);
        if (months == 0)
            return;

        var user = await userRepository.Get(u => u.Email == UsersSeeder.AdminEmail, ct: ct)
            ?? throw new InvalidOperationException("O seed demo precisa do Admin criado pelo UsersSeeder.");

        var stores = (await storeRepository.GetAll(s => DemoCatalog.StoreNames.Contains(s.Name), 0, int.MaxValue,
                q => q.Include(s => s.Locations), ct: ct))
            .OrderBy(s => Array.IndexOf(DemoCatalog.StoreNames, s.Name))
            .ToList();
        if (stores.Count == 0)
            return;

        var locationIds = stores.SelectMany(s => s.Locations).Select(l => l.Id).ToList();
        if (await movementRepository.Exists(m => locationIds.Contains(m.LocationId), ct))
            return;

        var stopwatch = Stopwatch.StartNew();
        var simulation = new DemoHistorySimulation(Anchor(), months, stores.Count, new Random(RandomSeed));
        simulation.Run();

        await Persist(new Plan(simulation, stores, user.Id, await SupplierIds(ct)), ct);

        logger.LogInformation(
            "Histórico demo: {Sales} vendas e {Purchases} compras de {First} a {Last} ({Lost} un. de venda perdida) em {Seconds} s.",
            simulation.Sales.Count, simulation.Purchases.Count, simulation.FirstDay, simulation.Anchor.AddDays(-1),
            simulation.LostUnits, stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture));
    }

    private DateOnly Anchor()
        => DateOnly.TryParse(configuration[AnchorDateKey], CultureInfo.InvariantCulture, out var anchor) ? anchor : clock.Today;

    private async Task<List<Guid>> SupplierIds(CancellationToken ct)
    {
        var documents = DemoCatalog.Suppliers.Select(s => TaxDocument.ParseCnpj(s.Cnpj)).ToList();
        var byCnpj = (await supplierRepository.GetAll(s => documents.Contains(s.Cnpj), 0, int.MaxValue, ct: ct))
            .ToDictionary(s => s.Cnpj.Value, s => s.Id);
        return documents.Select(d => byCnpj[d.Value]).ToList();
    }

    private async Task Persist(Plan plan, CancellationToken ct)
    {
        var simulation = plan.Simulation;
        var salesByDay = simulation.Sales.ToLookup(s => s.Day);
        var receiptsByDay = simulation.Purchases.Where(p => p.ReceiveDay != null).ToLookup(p => p.ReceiveDay!.Value);
        var openByDay = simulation.Purchases.Where(p => p.ReceiveDay == null).ToLookup(p => p.OrderDay);
        var lastCosts = new Dictionary<(int Supplier, string Product), (decimal Cost, DateTime At)>();

        for (var day = 0; day < simulation.Days; day++)
        {
            var startOfDay = clock.StartOfDayUtc(simulation.DateOf(day));
            var variants = await LoadVariants(plan, ct);

            if (day == 0)
                await PostOpening(plan, variants, startOfDay.AddHours(7), ct);

            foreach (var purchase in receiptsByDay[day])
            {
                await Receive(plan, purchase, variants, ct);
                foreach (var line in purchase.Lines)
                    lastCosts[(purchase.Supplier, simulation.Items[line.Item].Product.Name)] = (line.UnitCost, startOfDay.AddHours(10));
            }

            foreach (var purchase in openByDay[day])
                await AddPurchaseOrder(plan, purchase, variants, ct);

            foreach (var sale in salesByDay[day])
                await AddSale(plan, sale, variants, startOfDay.AddMinutes(sale.Minute), ct);

            await CommitAndClear(ct);
        }

        await RecordLastCosts(plan, lastCosts, ct);
    }

    /// <summary>Variações rastreadas do dia: as mesmas instâncias que o livro-razão atualiza (custo médio vigente).</summary>
    private async Task<Dictionary<string, ProductVariant>> LoadVariants(Plan plan, CancellationToken ct)
        => (await variantRepository.GetAllForUpdate(v => plan.Skus.Contains(v.Sku), ct)).ToDictionary(v => v.Sku);

    private async Task PostOpening(Plan plan, Dictionary<string, ProductVariant> variants, DateTime at, CancellationToken ct)
    {
        var simulation = plan.Simulation;
        var openedOn = simulation.DateOf(0);
        var lines = new List<StockLineDto>();
        for (var s = 0; s < plan.Stores.Count; s++)
        {
            for (var i = 0; i < simulation.Items.Count; i++)
            {
                var item = simulation.Items[i];
                var variant = variants[item.Variant.Sku];
                var cost = item.Variant.Cost;
                // Alimento controla lote (F1.7): um lote de abertura por variação, o mesmo nos dois locais das duas lojas.
                var lotId = await LotFor(variant.Id, item.Product, DemoCatalog.OpeningLotCode, openedOn, ct);
                AddOpeningLine(lines, variant, plan.SaleLocation(s), simulation.SaleOpening[s, i], cost, lotId);
                AddOpeningLine(lines, variant, plan.DepositLocation(s), simulation.DepositOpening[s, i], cost, lotId);
            }
        }

        if (lines.Count == 0)
            return;

        await Post(new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = plan.UserId, Note = OpeningNote }, lines, ct);
        Stamp(at);
        // Sem limpar o rastreador: as variações do dia seguem sendo as instâncias que o livro-razão atualiza.
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>
    /// Lote de uma chegada do alimento (F1.7), vencendo na vida útil a partir do dia da chegada; nulo para o que não é
    /// alimento. Mesmo código no mesmo dia (duas lojas recebendo a mesma variação) reaproveita o lote.
    /// </summary>
    private async Task<Guid?> LotFor(Guid variantId, DemoProduct product, string code, DateOnly arrivedOn, CancellationToken ct)
    {
        if (DemoCatalog.ShelfLifeDays(product) is not { } shelfLife)
            return null;
        if (_lots.TryGetValue((variantId, code), out var existing))
            return existing;

        var lot = new Lot(variantId, code, arrivedOn.AddDays(shelfLife));
        await lotRepository.AddAsync(lot, ct);
        _lots[(variantId, code)] = lot.Id;
        return lot.Id;
    }

    private static void AddOpeningLine(List<StockLineDto> lines, ProductVariant variant, Guid locationId, int quantity, decimal cost, Guid? lotId)
    {
        if (quantity <= 0)
            return;
        lines.Add(new StockLineDto
        {
            VariantId = variant.Id,
            LocationId = locationId,
            LotId = lotId,
            Type = MovementTypeEnum.In,
            Reason = MovementReasonEnum.Opening,
            Quantity = quantity,
            UnitCost = cost,
        });
    }

    /// <summary>
    /// Como o recebimento da F2.8: entrada In/Purchase por item no salão, com o custo (recalcula o médio), médio após a
    /// entrada no item e lead time real. Commit logo em seguida: o médio da próxima entrada lê o saldo da empresa no banco.
    /// </summary>
    private async Task Receive(Plan plan, DemoHistorySimulation.Purchase purchase, Dictionary<string, ProductVariant> variants,
        CancellationToken ct)
    {
        var order = await AddPurchaseOrder(plan, purchase, variants, ct);
        var arrivedOn = plan.Simulation.DateOf(purchase.ReceiveDay!.Value);
        var products = purchase.Lines.ToDictionary(
            line => variants[plan.Simulation.Items[line.Item].Variant.Sku].Id, line => plan.Simulation.Items[line.Item].Product);
        var lines = new List<StockLineDto>();
        foreach (var item in order.Items.OrderBy(i => i.SortOrder))
        {
            lines.Add(new StockLineDto
            {
                VariantId = item.VariantId,
                LocationId = order.ReceiveLocationId,
                LotId = await LotFor(item.VariantId, products[item.VariantId], DemoCatalog.PurchaseLotCode(arrivedOn), arrivedOn, ct),
                Type = MovementTypeEnum.In,
                Reason = MovementReasonEnum.Purchase,
                Quantity = item.Quantity,
                UnitCost = item.UnitCost,
            });
        }

        await Post(new StockDocumentDto { SourceType = SourceTypeEnum.PurchaseOrder, SourceId = order.Id, UserId = plan.UserId }, lines, ct);

        var byId = variants.Values.ToDictionary(v => v.Id);
        foreach (var item in order.Items)
            item.RecordReceipt(byId[item.VariantId].AverageCost);

        var sentOn = plan.Simulation.DateOf(purchase.OrderDay);
        var receivedOn = plan.Simulation.DateOf(purchase.ReceiveDay!.Value);
        var receivedAt = clock.StartOfDayUtc(receivedOn).AddHours(10);
        order.Receive(plan.UserId, receivedAt, receivedOn, sentOn);
        Stamp(receivedAt);
        await unitOfWork.CommitAsync(ct);
    }

    /// <summary>Pedido criado e enviado no dia da revisão; o que chega antes da âncora é recebido pelo <see cref="Receive"/>.</summary>
    private async Task<PurchaseOrder> AddPurchaseOrder(Plan plan, DemoHistorySimulation.Purchase purchase,
        Dictionary<string, ProductVariant> variants, CancellationToken ct)
    {
        var simulation = plan.Simulation;
        var dto = new SavePurchaseOrderDto
        {
            SupplierId = plan.SupplierIds[purchase.Supplier],
            ReceiveLocationId = plan.SaleLocation(purchase.Store),
            Note = PurchaseNote,
            Items = purchase.Lines.Select(line => new SavePurchaseItemDto
            {
                VariantId = variants[simulation.Items[line.Item].Variant.Sku].Id,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
            }).ToList(),
        };

        var sentOn = simulation.DateOf(purchase.OrderDay);
        var sentAt = clock.StartOfDayUtc(sentOn).AddHours(9);
        var order = new PurchaseOrder(dto, plan.Stores[purchase.Store].Id, plan.UserId,
            DemoCatalog.Suppliers[purchase.Supplier].LeadTimeDays, sentOn);
        order.Send(sentAt, sentOn);
        await purchaseOrderRepository.AddAsync(order, ct);
        Stamp(sentAt);
        return order;
    }

    /// <summary>Como a venda rápida (F2.4): pedido Pago/Counter com o preço da variação, um pagamento e a baixa Out/Sale.</summary>
    private async Task AddSale(Plan plan, DemoHistorySimulation.Sale sale, Dictionary<string, ProductVariant> variants, DateTime paidAt,
        CancellationToken ct)
    {
        var saleLocationId = plan.SaleLocation(sale.Store);
        var order = new SaleOrder(new QuickSaleDto { StoreId = plan.Stores[sale.Store].Id, SaleLocationId = saleLocationId },
            saleLocationId, plan.UserId, paidAt);

        foreach (var line in sale.Lines)
        {
            var variant = variants[plan.Simulation.Items[line.Item].Variant.Sku];
            order.AddItem(new OrderItem(new QuickSaleItemDto { VariantId = variant.Id, Quantity = line.Quantity }, variant, variant.Price, null));
        }
        order.AddPayment(new Payment(new QuickSalePaymentDto { Method = sale.Method, Amount = order.Total }));

        var lines = order.Items.OrderBy(i => i.Position).Select(i => new StockLineDto
        {
            VariantId = i.VariantId,
            LocationId = saleLocationId,
            Type = MovementTypeEnum.Out,
            Reason = MovementReasonEnum.Sale,
            Quantity = i.Quantity,
        }).ToList();

        await Post(new StockDocumentDto { SourceType = SourceTypeEnum.Order, SourceId = order.Id, UserId = plan.UserId }, lines, ct);
        await orderRepository.AddAsync(order, ct);
        Stamp(paidAt);
    }

    private async Task Post(StockDocumentDto document, List<StockLineDto> lines, CancellationToken ct)
    {
        var failures = await stockLedger.Post(document, lines, ct);
        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Histórico demo recusado pelo livro-razão: {string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"))}");
    }

    /// <summary>Último custo no vínculo produto × fornecedor (o da variação o livro-razão já gravou a cada entrada).</summary>
    private async Task RecordLastCosts(Plan plan, Dictionary<(int Supplier, string Product), (decimal Cost, DateTime At)> lastCosts,
        CancellationToken ct)
    {
        if (lastCosts.Count == 0)
            return;

        var links = await context.Set<ProductSupplier>()
            .Include(ps => ps.Product)
            .Where(ps => plan.SupplierIds.Contains(ps.SupplierId))
            .ToListAsync(ct);

        foreach (var link in links)
        {
            if (lastCosts.TryGetValue((plan.SupplierIds.IndexOf(link.SupplierId), link.Product.Name), out var last))
                link.RecordLastCost(last.Cost, last.At);
        }

        await CommitAndClear(ct);
    }

    /// <summary>Data simulada no <c>CreatedAt</c> de tudo que entrou no rastreador desde o último ajuste.</summary>
    private void Stamp(DateTime at)
    {
        foreach (var entry in context.ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added && _stamped.Add(entry.Entity))
                entry.Property(nameof(BaseModel.CreatedAt)).CurrentValue = at;
        }
    }

    private async Task CommitAndClear(CancellationToken ct)
    {
        await unitOfWork.CommitAsync(ct);
        context.ChangeTracker.Clear();
        _stamped.Clear();
    }

    /// <summary>Ids resolvidos uma vez: lojas, locais, fornecedores e SKUs do catálogo demo.</summary>
    private sealed class Plan(DemoHistorySimulation simulation, List<Store> stores, Guid userId, List<Guid> supplierIds)
    {
        public DemoHistorySimulation Simulation { get; } = simulation;
        public List<Store> Stores { get; } = stores;
        public Guid UserId { get; } = userId;
        public List<Guid> SupplierIds { get; } = supplierIds;
        public List<string> Skus { get; } = simulation.Items.Select(i => i.Variant.Sku).ToList();

        public Guid SaleLocation(int store) => Stores[store].DefaultSaleLocationId!.Value;

        public Guid DepositLocation(int store) => Stores[store].Locations.First(l => !l.IsSellable).Id;
    }
}
