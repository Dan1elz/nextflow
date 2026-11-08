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
/// pedidos de compra recebidos) com as datas no passado. Parte das vendas leva um cliente do <see cref="DemoRegistry"/>. Saldo = soma dos movimentos por construção (RN-CE01, RN-CE14).
/// <para>
/// Roda se nenhum local das lojas demo tem movimento (a abertura da F1.11 não roda com histórico ligado). Cada dia é gravado
/// numa transação (tudo ou nada), com o rastreador limpo depois, para a memória não crescer com 36 meses de registros. O
/// <c>CreatedAt</c> dos registros novos é ajustado no <see cref="AppDbContext"/> para a data simulada: só o seed faz isso.
/// </para>
/// <para>
/// Retomada: se o startup cai no meio (restart do container, erro), o próximo startup refaz a simulação com a mesma âncora,
/// gravada na nota da abertura, e continua do dia seguinte ao último gravado. Como a simulação é determinística, o resultado
/// é o mesmo de um seed sem interrupção. Abertura sem âncora na nota (histórico anterior a 08/11/2026) não é retomada.
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
    IClientRepository clientRepository,
    IStockLedger stockLedger,
    ILotRepository lotRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    ILogger<DemoHistorySeeder> logger) : ISeeder
{
    public const string HistoryMonthsKey = "Seed:Demo:HistoryMonths";
    public const string AnchorDateKey = "Seed:Demo:AnchorDate";
    /// <summary>Começo da nota da abertura; o resto é a âncora (<see cref="OpeningNote"/>), lida na retomada.</summary>
    public const string OpeningNotePrefix = "Saldo de abertura do histórico (demonstração";
    public const string PurchaseNote = "Reposição programada (dados de demonstração)";
    public const int RandomSeed = 42;

    private readonly HashSet<BaseModel> _stamped = [];
    private readonly Dictionary<(Guid VariantId, string Code), Guid> _lots = [];

    /// <summary>Sorteio do cliente da venda, separado do da simulação para não mudar o plano de vendas e compras.</summary>
    private readonly Random _clientRandom = new(RandomSeed + 1);

    /// <summary>Sorteio do vendedor da venda, também separado da simulação.</summary>
    private readonly Random _sellerRandom = new(RandomSeed + 2);

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
        var anchor = Anchor();
        var startDay = 0;
        if (await movementRepository.Exists(m => locationIds.Contains(m.LocationId), ct))
        {
            if (await ResumePoint(stores, locationIds, months, ct) is not { } resume)
                return;
            (anchor, startDay) = resume;
        }

        var stopwatch = Stopwatch.StartNew();
        var simulation = new DemoHistorySimulation(anchor, months, stores.Count, new Random(RandomSeed));
        simulation.Run();

        var plan = new Plan(simulation, stores, user.Id, await SupplierIds(ct), await WeightedClientIds(ct),
            await WeightedSellerIds(user.Id, stores, ct));
        await Persist(plan, startDay, ct);

        logger.LogInformation(
            "Histórico demo: {Sales} vendas e {Purchases} compras de {First} a {Last} ({Lost} un. de venda perdida) em {Seconds} s.",
            simulation.Sales.Count, simulation.Purchases.Count, simulation.DateOf(startDay), simulation.Anchor.AddDays(-1),
            simulation.LostUnits, stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture));
    }

    private DateOnly Anchor()
        => DateOnly.TryParse(configuration[AnchorDateKey], CultureInfo.InvariantCulture, out var anchor) ? anchor : clock.Today;

    /// <summary>Nota da abertura com a âncora da simulação, para a retomada refazer o mesmo plano.</summary>
    public static string OpeningNote(DateOnly anchor)
        => $"{OpeningNotePrefix}, âncora {anchor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})";

    /// <summary>
    /// Histórico já começado: a âncora (da nota da abertura) e o primeiro dia ainda não gravado. Nulo quando não há o que
    /// retomar: histórico completo, abertura da F1.11 ou anterior à retomada (sem âncora), ou janela de meses diferente.
    /// Só conta o que foi gravado antes da âncora, então venda feita à mão depois do seed não esconde um histórico pela metade.
    /// </summary>
    private async Task<(DateOnly Anchor, int StartDay)?> ResumePoint(List<Store> stores, List<Guid> locationIds, int months,
        CancellationToken ct)
    {
        var opening = await context.Set<StockMovement>()
            .Where(m => locationIds.Contains(m.LocationId) && m.Reason == MovementReasonEnum.Opening
                && m.Note != null && m.Note.StartsWith(OpeningNotePrefix))
            .Select(m => new { m.Note, m.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (opening is null)
            return null;

        var match = System.Text.RegularExpressions.Regex.Match(opening.Note!, @"âncora (\d{4}-\d{2}-\d{2})");
        if (!match.Success || !DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var anchor))
            return null;

        var firstDay = new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(-months);
        if (clock.ToBusinessDate(opening.CreatedAt) != firstDay)
        {
            logger.LogWarning("Histórico demo começado com outra janela de meses: não é retomado. Para refazer, apague o banco.");
            return null;
        }

        var end = clock.StartOfDayUtc(anchor);
        var storeIds = stores.Select(s => s.Id).ToList();
        var lastMovement = await context.Set<StockMovement>()
            .Where(m => locationIds.Contains(m.LocationId) && m.CreatedAt < end).MaxAsync(m => (DateTime?)m.CreatedAt, ct);
        var lastOrder = await context.Set<SaleOrder>()
            .Where(o => storeIds.Contains(o.StoreId) && o.CreatedAt < end).MaxAsync(o => (DateTime?)o.CreatedAt, ct);
        var lastPurchase = await context.Set<PurchaseOrder>()
            .Where(p => storeIds.Contains(p.StoreId) && p.CreatedAt < end).MaxAsync(p => (DateTime?)p.CreatedAt, ct);
        var last = new[] { lastMovement, lastOrder, lastPurchase }.Max()!.Value;

        var startDay = clock.ToBusinessDate(last).DayNumber - firstDay.DayNumber + 1;
        if (startDay >= anchor.DayNumber - firstDay.DayNumber)
            return null;

        logger.LogWarning("Histórico demo interrompido em {Last}: retomando do dia seguinte até {Anchor}.",
            clock.ToBusinessDate(last), anchor.AddDays(-1));
        return (anchor, startDay);
    }

    private async Task<List<Guid>> SupplierIds(CancellationToken ct)
    {
        var documents = DemoCatalog.Suppliers.Select(s => TaxDocument.ParseCnpj(s.Cnpj)).ToList();
        var byCnpj = (await supplierRepository.GetAll(s => documents.Contains(s.Cnpj), 0, int.MaxValue, ct: ct))
            .ToDictionary(s => s.Cnpj.Value, s => s.Id);
        return documents.Select(d => byCnpj[d.Value]).ToList();
    }

    /// <summary>Clientes demo com compra, cada um repetido pelo peso (o sorteio é uniforme na lista).</summary>
    private async Task<List<Guid>> WeightedClientIds(CancellationToken ct)
    {
        var documents = DemoRegistry.Clients.Select(c => TaxDocument.ParseCpf(c.Cpf)).ToList();
        var byCpf = (await clientRepository.GetAll(c => c.Cpf != null && documents.Contains(c.Cpf), 0, int.MaxValue, ct: ct))
            .ToDictionary(c => c.Cpf!.Value, c => c.Id);
        return DemoRegistry.Clients
            .SelectMany((client, i) => byCpf.TryGetValue(documents[i].Value, out var id) ? Enumerable.Repeat(id, client.Weight) : [])
            .ToList();
    }

    /// <summary>Vendedores por loja, cada um repetido pelo peso; o Admin vende em todas.</summary>
    private async Task<List<List<Guid>>> WeightedSellerIds(Guid adminId, List<Store> stores, CancellationToken ct)
    {
        var emails = DemoRegistry.Sellers.Select(s => s.Email).ToList();
        var byEmail = (await userRepository.GetAll(u => emails.Contains(u.Email), 0, int.MaxValue, ct: ct))
            .ToDictionary(u => u.Email, u => u.Id);

        return stores.Select((_, store) => Enumerable.Repeat(adminId, DemoRegistry.AdminSellerWeight)
                .Concat(DemoRegistry.Sellers
                    .Where(s => s.Stores.Contains(store) && byEmail.ContainsKey(s.Email))
                    .SelectMany(s => Enumerable.Repeat(byEmail[s.Email], s.Weight)))
                .ToList())
            .ToList();
    }

    /// <param name="startDay">Primeiro dia a gravar; os anteriores (já gravados) só repetem os sorteios, para a retomada dar o mesmo resultado.</param>
    private async Task Persist(Plan plan, int startDay, CancellationToken ct)
    {
        var simulation = plan.Simulation;
        var salesByDay = simulation.Sales.ToLookup(s => s.Day);
        var receiptsByDay = simulation.Purchases.Where(p => p.ReceiveDay != null).ToLookup(p => p.ReceiveDay!.Value);
        var openByDay = simulation.Purchases.Where(p => p.ReceiveDay == null).ToLookup(p => p.OrderDay);
        var lastCosts = new Dictionary<(int Supplier, string Product), (decimal Cost, DateTime At)>();

        for (var day = 0; day < simulation.Days; day++)
        {
            var startOfDay = clock.StartOfDayUtc(simulation.DateOf(day));
            foreach (var purchase in receiptsByDay[day])
            {
                foreach (var line in purchase.Lines)
                    lastCosts[(purchase.Supplier, simulation.Items[line.Item].Product.Name)] = (line.UnitCost, startOfDay.AddHours(10));
            }

            if (day < startDay)
            {
                foreach (var sale in salesByDay[day])
                    PickParties(plan, sale);
                continue;
            }

            var current = day;
            await unitOfWork.ExecuteInTransactionAsync(async dayCt =>
            {
                var variants = await LoadVariants(plan, dayCt);

                if (current == 0)
                    await PostOpening(plan, variants, startOfDay.AddHours(7), dayCt);

                foreach (var purchase in receiptsByDay[current])
                    await Receive(plan, purchase, variants, dayCt);

                foreach (var purchase in openByDay[current])
                    await AddPurchaseOrder(plan, purchase, variants, dayCt);

                foreach (var sale in salesByDay[current])
                    await AddSale(plan, sale, variants, startOfDay.AddMinutes(sale.Minute), dayCt);

                await CommitAndClear(dayCt);
            }, ct);
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

        await Post(new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = plan.UserId, Note = OpeningNote(simulation.Anchor) }, lines, ct);
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
        var (clientId, sellerId) = PickParties(plan, sale);
        var order = new SaleOrder(
            new QuickSaleDto { StoreId = plan.Stores[sale.Store].Id, SaleLocationId = saleLocationId, ClientId = clientId },
            saleLocationId, sellerId, paidAt);

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

        await Post(new StockDocumentDto { SourceType = SourceTypeEnum.Order, SourceId = order.Id, UserId = sellerId }, lines, ct);
        await orderRepository.AddAsync(order, ct);
        Stamp(paidAt);
    }

    /// <summary>Cliente e vendedor da venda, sempre nesta ordem de sorteio (a retomada repete os sorteios dos dias já gravados).</summary>
    private (Guid? ClientId, Guid SellerId) PickParties(Plan plan, DemoHistorySimulation.Sale sale)
    {
        var clientId = PickClient(plan);
        var sellers = plan.SellerIds[sale.Store];
        return (clientId, sellers[_sellerRandom.Next(sellers.Count)]);
    }

    /// <summary>Cliente identificado em parte das vendas (<see cref="DemoRegistry.IdentifiedSaleShare"/>); o resto é balcão anônimo.</summary>
    private Guid? PickClient(Plan plan)
        => plan.ClientIds.Count > 0 && _clientRandom.NextDouble() < DemoRegistry.IdentifiedSaleShare
            ? plan.ClientIds[_clientRandom.Next(plan.ClientIds.Count)]
            : null;

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
    private sealed class Plan(DemoHistorySimulation simulation, List<Store> stores, Guid userId, List<Guid> supplierIds, List<Guid> clientIds,
        List<List<Guid>> sellerIds)
    {
        /// <summary>Vendedores sorteáveis por loja, repetidos pelo peso.</summary>
        public List<List<Guid>> SellerIds { get; } = sellerIds;

        public DemoHistorySimulation Simulation { get; } = simulation;
        public List<Store> Stores { get; } = stores;
        public Guid UserId { get; } = userId;
        public List<Guid> SupplierIds { get; } = supplierIds;
        public List<Guid> ClientIds { get; } = clientIds;
        public List<string> Skus { get; } = simulation.Items.Select(i => i.Variant.Sku).ToList();

        public Guid SaleLocation(int store) => Stores[store].DefaultSaleLocationId!.Value;

        public Guid DepositLocation(int store) => Stores[store].Locations.First(l => !l.IsSellable).Id;
    }
}
