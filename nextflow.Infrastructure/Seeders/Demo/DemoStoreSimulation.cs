namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Simula, só em memória, a operação da loja dia a dia: abertura de estoque, revisões periódicas de compra por fornecedor
/// (com lead time real e atrasos), recebimentos e vendas limitadas ao estoque disponível (ruptura = venda perdida).
/// O resultado é um plano consistente: o estoque final de cada produto é exatamente
/// abertura + entradas − vendas, que é o que o seeder grava como movimentações (RNF002).
/// </summary>
internal sealed class DemoStoreSimulation
{
    public const int HistoryMonths = 36;
    private const int ReviewIntervalDays = 28;
    // Cobertura extra da reposição: com revisão a cada 28 dias, mantém o estoque acima do que o motor
    // de previsão pede (demanda de lead time + 30 dias + segurança) em qualquer ponto do ciclo
    private const int SafetyDays = 40;
    private const int SeasonReturnDelayDays = 7;
    private const int NewProductAgeDays = 100;
    private const int StockoutBlockDays = 110;

    public sealed record Opening(int Product, int Day, int Quantity, decimal Cost);
    public sealed record SupplierReturn(int Product, int Day, int Quantity, decimal Cost);
    public sealed record PurchaseLine(int Product, int Quantity, decimal Cost);
    public sealed record Purchase(int Supplier, int OrderDay, int ReceiveDay, bool Received, string Note, List<PurchaseLine> Lines);
    public sealed record PromotionPeriod(int Product, DateOnly Start, DateOnly End, decimal DiscountPercent, string Reason);

    private readonly Random _rng;
    private readonly DemoProduct[] _products = DemoStoreCatalog.Products;

    public DateOnly Anchor { get; }
    public DateOnly FirstDay { get; }
    public DateOnly LastDay { get; }
    public int Days { get; }

    public List<Opening> Openings { get; } = [];
    /// <summary>Sobras de produtos de temporada devolvidas ao fornecedor (movimentação de saída).</summary>
    public List<SupplierReturn> SupplierReturns { get; } = [];
    public List<Purchase> Purchases { get; } = [];
    public List<PromotionPeriod> Promotions { get; } = [];
    /// <summary>Unidades vendidas por produto e dia (já limitadas ao estoque).</summary>
    public int[,] Sold { get; }
    /// <summary>Demanda que não virou venda por falta de estoque.</summary>
    public int LostUnits { get; private set; }
    public int[] FinalStock { get; }
    public decimal[] LastCost { get; }
    public DateOnly[] LaunchDate { get; }

    public DemoStoreSimulation(DateOnly anchor, Random rng)
    {
        _rng = rng;
        Anchor = anchor;
        FirstDay = new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(-HistoryMonths);
        LastDay = anchor.AddDays(-1);
        Days = LastDay.DayNumber - FirstDay.DayNumber + 1;
        Sold = new int[_products.Length, Days];
        FinalStock = new int[_products.Length];
        LastCost = [.. _products.Select(p => p.Cost)];
        LaunchDate = [.. _products.Select(p => p.Scenario == DemoScenario.New ? anchor.AddDays(-NewProductAgeDays) : FirstDay)];
    }

    public DateOnly DateOf(int day) => FirstDay.AddDays(day);

    public void Run()
    {
        BuildPromotions();

        var stock = new int[_products.Length];
        var onOrder = new int[_products.Length];
        var extraPurchases = PlanExtraPurchases();
        var stockoutStart = (int)(Days * 0.45);
        var reviewOffset = Enumerable.Range(0, DemoStoreCatalog.Suppliers.Length).Select(_ => _rng.Next(ReviewIntervalDays)).ToArray();

        for (var d = 0; d < Days; d++)
        {
            var date = DateOf(d);

            // 1. Abertura de estoque no primeiro dia de cada produto (histórico ou lançamento)
            for (var p = 0; p < _products.Length; p++)
            {
                if (date != LaunchDate[p]) continue;

                var lead = DemoStoreCatalog.Suppliers[_products[p].SupplierIndex].LeadTimeDays;
                var quantity = RoundUp(Math.Max(ExpectedDemand(p, d, lead + ReviewIntervalDays + SafetyDays) + (double)_products[p].MinStock, 10));
                stock[p] += quantity;
                Openings.Add(new Opening(p, d, quantity, HistoricalValue(_products[p].Cost, date)));
            }

            // 2. Recebimentos do dia
            foreach (var purchase in Purchases.Where(x => x.Received && x.ReceiveDay == d))
            {
                foreach (var line in purchase.Lines)
                {
                    stock[line.Product] += line.Quantity;
                    onOrder[line.Product] -= line.Quantity;
                    LastCost[line.Product] = line.Cost;
                }
            }

            foreach (var (p, _, quantity, note) in extraPurchases.Where(x => x.Day == d))
            {
                var cost = HistoricalValue(_products[p].Cost, date);
                var supplier = _products[p].SupplierIndex;
                // Compra pontual já recebida no mesmo dia do registro (lead time fica nos pedidos normais)
                Purchases.Add(new Purchase(supplier, Math.Max(0, d - DemoStoreCatalog.Suppliers[supplier].LeadTimeDays), d, true, note,
                    [new PurchaseLine(p, quantity, cost)]));
                stock[p] += quantity;
                LastCost[p] = cost;
            }

            // 3. Revisão periódica de compra por fornecedor
            for (var s = 0; s < DemoStoreCatalog.Suppliers.Length; s++)
            {
                if ((d + reviewOffset[s]) % ReviewIntervalDays != 0) continue;

                var lead = DemoStoreCatalog.Suppliers[s].LeadTimeDays;
                var actualLead = Math.Max(1, lead + _rng.Next(-2, 4));
                var lines = new List<PurchaseLine>();

                for (var p = 0; p < _products.Length; p++)
                {
                    if (_products[p].SupplierIndex != s || date < LaunchDate[p] || IsReplenishmentBlocked(p, d, lead, stockoutStart))
                        continue;

                    var need = ExpectedDemand(p, d, lead + ReviewIntervalDays + SafetyDays) + (double)_products[p].MinStock;
                    var quantity = RoundUp(need - stock[p] - onOrder[p]);
                    if (quantity <= 0) continue;

                    lines.Add(new PurchaseLine(p, quantity, HistoricalValue(_products[p].Cost, date)));
                    onOrder[p] += quantity;
                }

                if (lines.Count == 0) continue;

                var receiveDay = d + actualLead;
                var received = receiveDay < Days;
                Purchases.Add(new Purchase(s, d, receiveDay, received,
                    received ? "Reposição programada (dados de demonstração)" : "Pedido em trânsito (dados de demonstração)", lines));
            }

            // 4. Devolução da sobra de temporada (prática comum com ovos de Páscoa, kits e panetones).
            //    Quem ficou com excesso de propósito (cenário Overstock) não devolve
            for (var p = 0; p < _products.Length; p++)
            {
                if (stock[p] == 0 || _products[p].Scenario == DemoScenario.Overstock || !IsSeasonReturnDay(_products[p].Profile, date))
                    continue;

                SupplierReturns.Add(new SupplierReturn(p, d, stock[p], HistoricalValue(_products[p].Cost, date)));
                stock[p] = 0;
            }

            // 5. Vendas do dia, limitadas ao estoque
            for (var p = 0; p < _products.Length; p++)
            {
                if (date < LaunchDate[p]) continue;

                var rate = DemoDemandModel.DailyRate(_products[p], date, Anchor);
                if (Promotions.Any(x => x.Product == p && date >= x.Start && date <= x.End))
                    rate *= 1.5; // efeito da promoção no volume

                var demand = rate > 0 ? Poisson(rate) : 0;
                var sold = Math.Min(demand, stock[p]);
                Sold[p, d] = sold;
                stock[p] -= sold;
                LostUnits += demand - sold;
            }
        }

        Array.Copy(stock, FinalStock, stock.Length);
    }

    /// <summary>Validade do lote atual: próxima para o cenário de validade, senão a vida útil do produto.</summary>
    public DateOnly? Validity(int p) => _products[p].Scenario == DemoScenario.NearExpiry
        ? Anchor.AddDays(45)
        : _products[p].ShelfLifeDays is { } days ? Anchor.AddDays(days) : null;

    /// <summary>Preço/custo na data: ~6% a.a. de reajuste em relação ao valor atual.</summary>
    public decimal HistoricalValue(decimal currentValue, DateOnly date)
    {
        var factor = Math.Pow(1.06, (date.DayNumber - Anchor.DayNumber) / 365.0);
        return Math.Round(currentValue * (decimal)factor, 2, MidpointRounding.AwayFromZero);
    }

    private static bool IsSeasonReturnDay(DemandProfile profile, DateOnly date)
    {
        var seasonEnd = profile switch
        {
            DemandProfile.EasterEgg => Domain.Utils.CommercialCalendar.Easter(date.Year),
            DemandProfile.MothersKit => Domain.Utils.CommercialCalendar.MothersDay(date.Year),
            DemandProfile.ValentinesKit => Domain.Utils.CommercialCalendar.ValentinesDay(date.Year),
            DemandProfile.Panettone or DemandProfile.ChristmasHamper => Domain.Utils.CommercialCalendar.Christmas(date.Year - 1).AddDays(6),
            _ => (DateOnly?)null
        };
        return seasonEnd?.AddDays(SeasonReturnDelayDays) == date;
    }

    private bool IsReplenishmentBlocked(int p, int day, int lead, int stockoutStart) => _products[p].Scenario switch
    {
        // Comprar: a última reposição não foi feita, então o produto chega ao horizonte com pouco estoque
        DemoScenario.Buy => day >= Days - (lead + ReviewIntervalDays + 10),
        // Ruptura histórica: o fornecedor falhou e o produto ficou semanas zerado no meio do histórico
        DemoScenario.Stockout => day >= stockoutStart && day < stockoutStart + StockoutBlockDays,
        _ => false
    };

    /// <summary>Compras pontuais que criam excesso (Promover por baixa temporada ou por validade).</summary>
    private List<(int Product, int Day, int Quantity, string Note)> PlanExtraPurchases()
    {
        var result = new List<(int, int, int, string)>();

        for (var p = 0; p < _products.Length; p++)
        {
            switch (_products[p].Scenario)
            {
                case DemoScenario.Overstock when _products[p].Profile == DemandProfile.EasterEgg:
                {
                    // Encalhe da última Páscoa: compra extra na véspera que não vendeu
                    var easter = Domain.Utils.CommercialCalendar.Easter(Anchor.Year);
                    if (easter >= Anchor) easter = Domain.Utils.CommercialCalendar.Easter(Anchor.Year - 1);
                    var day = easter.AddDays(-12).DayNumber - FirstDay.DayNumber;
                    result.Add((p, day, RoundUp(_products[p].BaseMonthly * 0.35), "Compra extra para a Páscoa (dados de demonstração)"));
                    break;
                }
                case DemoScenario.Overstock:
                {
                    var day = Days - 20;
                    result.Add((p, day, RoundUp(2.5 * ExpectedDemand(p, Days, 90)), "Compra de oportunidade (dados de demonstração)"));
                    break;
                }
                case DemoScenario.NearExpiry:
                {
                    var day = Days - 30;
                    result.Add((p, day, RoundUp(2.0 * ExpectedDemand(p, Days, 45)), "Lote promocional do fornecedor (dados de demonstração)"));
                    break;
                }
            }
        }

        return [.. result.Where(x => x.Item2 >= 0 && x.Item2 < Days && x.Item3 > 0)];
    }

    private void BuildPromotions()
    {
        foreach (var definition in DemoStoreCatalog.PastPromotions)
        {
            var p = Array.FindIndex(_products, x => x.Code == definition.ProductCode);
            var (start, end) = LastWindowBefore(definition.Window);
            if (start < LaunchDate[p] || end >= Anchor)
                continue;

            Promotions.Add(new PromotionPeriod(p, start, end, definition.DiscountPercent, definition.Reason));
        }
    }

    private (DateOnly Start, DateOnly End) LastWindowBefore(DemoPromotionWindow window)
    {
        for (var year = Anchor.Year; year > Anchor.Year - 3; year--)
        {
            var (start, end) = window switch
            {
                DemoPromotionWindow.BlackFriday => (Domain.Utils.CommercialCalendar.BlackFriday(year).AddDays(-3), Domain.Utils.CommercialCalendar.BlackFriday(year).AddDays(2)),
                DemoPromotionWindow.SummerClearance => (new DateOnly(year, 1, 10), new DateOnly(year, 2, 8)),
                DemoPromotionWindow.ChildrensDay => (Domain.Utils.CommercialCalendar.ChildrensDay(year).AddDays(-7), Domain.Utils.CommercialCalendar.ChildrensDay(year)),
                DemoPromotionWindow.PostEaster => (Domain.Utils.CommercialCalendar.Easter(year).AddDays(1), Domain.Utils.CommercialCalendar.Easter(year).AddDays(10)),
                _ => throw new ArgumentOutOfRangeException(nameof(window), window, null)
            };

            if (end < Anchor.AddDays(-7))
                return (start, end);
        }

        return (DateOnly.MinValue, DateOnly.MinValue);
    }

    private double ExpectedDemand(int p, int fromDay, int days)
    {
        var total = 0.0;
        for (var d = fromDay; d < fromDay + days; d++)
            total += DemoDemandModel.DailyRate(_products[p], DateOf(d), Anchor);
        return total;
    }

    private static int RoundUp(double quantity) => quantity <= 0 ? 0 : (int)Math.Ceiling(quantity / 10.0) * 10;

    private int Poisson(double lambda)
    {
        if (lambda > 30)
        {
            // Aproximação normal para taxas altas
            var u1 = 1.0 - _rng.NextDouble();
            var u2 = _rng.NextDouble();
            var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Max(0, (int)Math.Round(lambda + z * Math.Sqrt(lambda)));
        }

        // Algoritmo de Knuth
        var limit = Math.Exp(-lambda);
        var k = 0;
        var product = _rng.NextDouble();
        while (product > limit)
        {
            k++;
            product *= _rng.NextDouble();
        }
        return k;
    }
}
