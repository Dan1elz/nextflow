using Nextflow.Domain.Enums;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Simula em memória, dia a dia, a operação das lojas demo (F2.9): abertura no dia 0, revisão de compra por fornecedor e loja
/// com prazo real sorteado, recebimentos e vendas de balcão limitadas ao saldo do salão (ruptura = venda perdida). O plano é
/// coerente por construção: o saldo final de cada item é abertura + entradas − vendas, que é o que o seeder lança no
/// livro-razão. Determinístico para a mesma âncora e o mesmo <see cref="Random"/>.
/// </summary>
public sealed class DemoHistorySimulation
{
    public const int ReviewIntervalDays = 14;

    /// <summary>Cobertura além de prazo + intervalo: a reposição mira esse fôlego de dias de venda.</summary>
    private const int CoverDays = 28;

    private const int OpeningHour = 9;
    private const int ClosingHour = 20;
    private const int MaxLinesPerSale = 5;
    private const decimal CostNoise = 0.06m;

    /// <summary>Uma variação do catálogo demo com a demanda do produto e o peso dela na grade.</summary>
    public sealed record Item(DemoProduct Product, DemoVariant Variant, DemoDemandSpec Spec, double Weight);

    public sealed record SaleLine(int Item, int Quantity);

    /// <param name="Minute">Minuto do dia (no fuso de negócio) em que a venda foi paga.</param>
    public sealed record Sale(int Store, int Day, int Minute, PaymentMethodEnum Method, List<SaleLine> Lines);

    public sealed record PurchaseLine(int Item, int Quantity, decimal UnitCost);

    /// <param name="ReceiveDay">Nulo quando a chegada cai depois da âncora: o pedido fica Pendente.</param>
    public sealed record Purchase(int Store, int Supplier, int OrderDay, int? ReceiveDay, List<PurchaseLine> Lines);

    private readonly Random _rng;
    private readonly int _storeCount;

    public DateOnly Anchor { get; }
    public DateOnly FirstDay { get; }
    public int Days { get; }
    public List<Item> Items { get; } = [];

    /// <summary>Abertura do salão e do depósito por loja e item.</summary>
    public int[,] SaleOpening { get; }
    public int[,] DepositOpening { get; }

    public List<Sale> Sales { get; } = [];
    public List<Purchase> Purchases { get; } = [];
    public int LostUnits { get; private set; }

    public DemoHistorySimulation(DateOnly anchor, int months, int storeCount, Random rng)
    {
        _rng = rng;
        _storeCount = storeCount;
        Anchor = anchor;
        FirstDay = new DateOnly(anchor.Year, anchor.Month, 1).AddMonths(-months);
        Days = anchor.DayNumber - FirstDay.DayNumber;

        foreach (var product in DemoCatalog.Products)
        {
            if (!DemoDemand.ByCode.TryGetValue(product.Code, out var spec))
                continue;
            var variants = DemoCatalog.VariantsOf(product);
            for (var index = 0; index < variants.Count; index++)
                Items.Add(new Item(product, variants[index], spec, DemoDemand.GridWeight(variants.Count, index)));
        }

        SaleOpening = new int[storeCount, Items.Count];
        DepositOpening = new int[storeCount, Items.Count];
    }

    public DateOnly DateOf(int day) => FirstDay.AddDays(day);

    public void Run()
    {
        var stock = new int[_storeCount, Items.Count];
        var onOrder = new int[_storeCount, Items.Count];
        var pendingReceipts = new Dictionary<int, List<Purchase>>();

        for (var s = 0; s < _storeCount; s++)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                var lead = SupplierOf(i).LeadTimeDays;
                SaleOpening[s, i] = (int)Math.Ceiling(ExpectedDemand(s, i, 0, lead + ReviewIntervalDays + CoverDays) + MinSale(s, i));
                stock[s, i] = SaleOpening[s, i];
                // Depósito como na F1.11: o dobro do mínimo; fica parado (o seed não transfere).
                DepositOpening[s, i] = Items[i].Product.MinStock > 0 ? (int)Math.Ceiling((double)Items[i].Product.MinStock * StoreFactor(s) * 2) : 0;
            }
        }

        for (var d = 0; d < Days; d++)
        {
            if (pendingReceipts.Remove(d, out var arriving))
            {
                foreach (var purchase in arriving)
                {
                    foreach (var line in purchase.Lines)
                    {
                        stock[purchase.Store, line.Item] += line.Quantity;
                        onOrder[purchase.Store, line.Item] -= line.Quantity;
                    }
                }
            }

            Review(d, stock, onOrder, pendingReceipts);
            Sell(d, stock);
        }
    }

    private void Review(int d, int[,] stock, int[,] onOrder, Dictionary<int, List<Purchase>> pendingReceipts)
    {
        for (var s = 0; s < _storeCount; s++)
        {
            for (var sup = 0; sup < DemoCatalog.Suppliers.Length; sup++)
            {
                if ((d + sup * 3 + s * 7) % ReviewIntervalDays != 0)
                    continue;

                var supplier = DemoCatalog.Suppliers[sup];
                var lines = new List<PurchaseLine>();
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Product.SupplierIndex != sup)
                        continue;
                    // A última reposição "atrasou": o item chega ao fim do histórico com pouco saldo.
                    if (Items[i].Spec.LateLastOrder && d >= Days - (supplier.LeadTimeDays + ReviewIntervalDays + 10))
                        continue;

                    var target = ExpectedDemand(s, i, d, supplier.LeadTimeDays + ReviewIntervalDays + CoverDays) + MinSale(s, i);
                    var need = target - stock[s, i] - onOrder[s, i];
                    if (need < 1)
                        continue;

                    var quantity = ToOrderQuantity(need, supplier.OrderMultiple);
                    var noise = 1 + ((decimal)_rng.NextDouble() - 0.5m) * CostNoise;
                    lines.Add(new PurchaseLine(i, quantity, Math.Round(Items[i].Variant.Cost * noise, 2, MidpointRounding.AwayFromZero)));
                    onOrder[s, i] += quantity;
                }

                if (lines.Count == 0)
                    continue;

                var receiveDay = d + Math.Max(1, supplier.LeadTimeDays + _rng.Next(-2, 4));
                var purchase = new Purchase(s, sup, d, receiveDay < Days ? receiveDay : null, lines);
                Purchases.Add(purchase);
                if (purchase.ReceiveDay is { } day)
                {
                    if (!pendingReceipts.TryGetValue(day, out var list))
                        pendingReceipts[day] = list = [];
                    list.Add(purchase);
                }
            }
        }
    }

    private void Sell(int d, int[,] stock)
    {
        var date = DateOf(d);
        for (var s = 0; s < _storeCount; s++)
        {
            var lines = new List<SaleLine>();
            for (var i = 0; i < Items.Count; i++)
            {
                var rate = DemoDemand.DailyRate(Items[i].Spec, date, Anchor) * Items[i].Weight * StoreFactor(s);
                var demand = rate > 0 ? Poisson(rate) : 0;
                var sold = Math.Min(demand, stock[s, i]);
                LostUnits += demand - sold;
                if (sold == 0)
                    continue;
                stock[s, i] -= sold;
                lines.Add(new SaleLine(i, sold));
            }

            // Embaralha e agrupa as linhas do dia em vendas de 1 a 5 itens, cada uma num horário do expediente.
            for (var k = lines.Count - 1; k > 0; k--)
            {
                var j = _rng.Next(k + 1);
                (lines[k], lines[j]) = (lines[j], lines[k]);
            }

            var daySales = new List<Sale>();
            for (var start = 0; start < lines.Count;)
            {
                var size = Math.Min(_rng.Next(1, MaxLinesPerSale + 1), lines.Count - start);
                var minute = _rng.Next(OpeningHour * 60, ClosingHour * 60);
                daySales.Add(new Sale(s, d, minute, PickPaymentMethod(), lines.GetRange(start, size)));
                start += size;
            }
            Sales.AddRange(daySales.OrderBy(x => x.Minute));
        }
    }

    public DemoSupplier SupplierOf(int item) => DemoCatalog.Suppliers[Items[item].Product.SupplierIndex];

    private static double StoreFactor(int store) => store == 0 ? 1 : (double)DemoCatalog.SecondStoreFactor;

    private double MinSale(int store, int item)
        => Items[item].Product.MinStock > 0 ? Math.Ceiling((double)Items[item].Product.MinStock * StoreFactor(store)) : 0;

    /// <summary>Caixa do fornecedor (RN-CE30): múltiplo e, com caixa, pedido mínimo de duas caixas, como no vínculo da F1.11.</summary>
    private static int ToOrderQuantity(double need, decimal orderMultiple)
    {
        var multiple = (int)Math.Max(1, orderMultiple);
        var quantity = (int)Math.Ceiling(need / multiple) * multiple;
        return multiple > 1 ? Math.Max(quantity, multiple * 2) : quantity;
    }

    private double ExpectedDemand(int store, int item, int fromDay, int days)
    {
        var total = 0.0;
        for (var d = fromDay; d < fromDay + days; d++)
            total += DemoDemand.DailyRate(Items[item].Spec, DateOf(d), Anchor);
        return total * Items[item].Weight * StoreFactor(store);
    }

    private PaymentMethodEnum PickPaymentMethod()
    {
        var roll = _rng.Next(100);
        return roll < 40 ? PaymentMethodEnum.Pix
            : roll < 70 ? PaymentMethodEnum.CreditCard
            : roll < 85 ? PaymentMethodEnum.DebitCard
            : roll < 97 ? PaymentMethodEnum.Cash
            : PaymentMethodEnum.BankTransfer;
    }

    private int Poisson(double lambda)
    {
        if (lambda > 30)
        {
            // Aproximação normal para taxas altas.
            var u1 = 1.0 - _rng.NextDouble();
            var u2 = _rng.NextDouble();
            var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return Math.Max(0, (int)Math.Round(lambda + z * Math.Sqrt(lambda)));
        }

        // Algoritmo de Knuth.
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
