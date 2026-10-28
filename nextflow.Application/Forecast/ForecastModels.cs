using System.Globalization;
using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Mês de calendário (ano + mês) com aritmética simples.</summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Número sequencial do mês (para diferenças e comparações).</summary>
    public int Ordinal => Year * 12 + (Month - 1);

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    public DateOnly FirstDay => new(Year, Month, 1);

    public YearMonth AddMonths(int months)
    {
        var ordinal = Ordinal + months;
        return new YearMonth(ordinal / 12, ordinal % 12 + 1);
    }

    /// <summary>Quantidade de meses de <paramref name="from"/> até <paramref name="to"/> (to − from).</summary>
    public static int MonthsBetween(YearMonth from, YearMonth to) => to.Ordinal - from.Ordinal;

    /// <summary>"yyyy-MM" (formato do payload da IA).</summary>
    public string Key => $"{Year:D4}-{Month:D2}";

    /// <summary>"MM/yyyy" (formato dos gráficos).</summary>
    public string Label => $"{Month:D2}/{Year:D4}";

    public int CompareTo(YearMonth other) => Ordinal.CompareTo(other.Ordinal);

    public static bool operator <(YearMonth a, YearMonth b) => a.Ordinal < b.Ordinal;
    public static bool operator >(YearMonth a, YearMonth b) => a.Ordinal > b.Ordinal;
    public static bool operator <=(YearMonth a, YearMonth b) => a.Ordinal <= b.Ordinal;
    public static bool operator >=(YearMonth a, YearMonth b) => a.Ordinal >= b.Ordinal;
}

/// <summary>Parâmetros de uma execução do cálculo.</summary>
public sealed class ForecastCalculationContext
{
    /// <summary>Janela de histórico usada no cálculo (meses completos; mês corrente excluído).</summary>
    public const int HistoryWindowMonths = 24;

    public DateOnly Today { get; }
    public int HorizonMonths { get; }

    /// <summary>Último mês completo (mês anterior ao corrente) — fim da série histórica.</summary>
    public YearMonth LastCompleteMonth { get; }

    /// <summary>Mês corrente (projeção usa só a fração restante de dias).</summary>
    public YearMonth CurrentMonth { get; }

    /// <summary>Meses do horizonte, a partir do próximo mês.</summary>
    public IReadOnlyList<YearMonth> Horizon { get; }

    public ForecastCalculationContext(DateOnly today, int horizonMonths)
    {
        if (horizonMonths is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(horizonMonths), "O horizonte deve estar entre 1 e 3 meses.");

        Today = today;
        HorizonMonths = horizonMonths;
        CurrentMonth = YearMonth.From(today);
        LastCompleteMonth = CurrentMonth.AddMonths(-1);
        Horizon = [.. Enumerable.Range(1, horizonMonths).Select(CurrentMonth.AddMonths)];
    }

    /// <summary>Mês correspondente à posição <paramref name="index"/> de uma série de <see cref="HistoryWindowMonths"/> meses.</summary>
    public YearMonth MonthAt(int index) => LastCompleteMonth.AddMonths(index - (HistoryWindowMonths - 1));
}

/// <summary>Entrada do cálculo para um produto (montada pelo caso de uso a partir do banco).</summary>
public sealed class ForecastProductInput
{
    public required Guid ProductId { get; init; }
    public required string Name { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string? Brand { get; init; }
    public UnitType UnitType { get; init; } = UnitType.Unit;

    /// <summary>Categoria de referência (para o índice sazonal da categoria).</summary>
    public string? ReferenceCategory { get; init; }

    public decimal CurrentStock { get; init; }

    /// <summary>Quantidade em pedidos de compra abertos (Orçamento/Pendente).</summary>
    public decimal IncomingStock { get; init; }

    public decimal MinStock { get; init; }

    /// <summary>Lead time em dias (média real dos últimos pedidos recebidos; fallback: prazo do fornecedor).</summary>
    public int LeadTimeDays { get; init; }

    /// <summary>true quando o lead time veio de pedidos reais recebidos.</summary>
    public bool LeadTimeFromHistory { get; init; }

    public decimal Price { get; init; }
    public decimal? CostPrice { get; init; }
    public DateOnly? Validity { get; init; }

    /// <summary>Desconto (%) da promoção vigente, se houver.</summary>
    public decimal? ActivePromotionDiscountPercent { get; init; }
    public DateOnly? ActivePromotionEndDate { get; init; }

    /// <summary>
    /// Vendas mensais (quantidade) dos últimos <see cref="ForecastCalculationContext.HistoryWindowMonths"/> meses completos,
    /// do mais antigo ao mais recente (último = mês anterior ao corrente). Meses sem venda = 0.
    /// </summary>
    public required IReadOnlyList<decimal> MonthlySales { get; init; }

    /// <summary>Meses desde a primeira venda (máx. 24).</summary>
    public int MonthsOfHistory { get; init; }

    /// <summary>Soma das vendas mensais dos produtos da categoria de referência (mesma janela), ou null.</summary>
    public IReadOnlyList<decimal>? CategoryMonthlySales { get; init; }

    /// <summary>Meses desde a primeira venda da categoria (máx. 24).</summary>
    public int CategoryMonthsOfHistory { get; init; }
}

public enum PromotionTrigger : byte
{
    None = 0,
    Excess = 1,    // excesso de estoque em baixa temporada
    Validity = 2   // validade próxima
}

/// <summary>Demanda projetada de um mês.</summary>
public sealed record MonthlyProjection(YearMonth Month, decimal Quantity, decimal SeasonalIndex);

/// <summary>Resultado determinístico do cálculo para um produto.</summary>
public sealed class ProductForecastResult
{
    public required ForecastProductInput Input { get; init; }

    public bool HasSales { get; init; }
    public int MonthsOfHistory { get; init; }
    public ForecastConfidence Confidence { get; init; }

    /// <summary>Índice sazonal final por mês-calendário (posição 0 = janeiro).</summary>
    public decimal[] SeasonalIndex { get; init; } = [];

    /// <summary>true quando o índice do produto (≥ 12 meses) entrou no cálculo.</summary>
    public bool HasProductSeasonality { get; init; }

    /// <summary>true quando o índice da categoria de referência entrou no cálculo.</summary>
    public bool HasCategorySeasonality { get; init; }

    /// <summary>Nível dessazonalizado (un./mês).</summary>
    public decimal Level { get; init; }

    /// <summary>Regra 5b: projeção baseada no mesmo mês do ano anterior (produto sazonal sem vendas recentes).</summary>
    public bool UsedSamePeriodLastYear { get; init; }

    /// <summary>Tendência anual aplicada na Regra 5b (últimos 12 meses ÷ 12 anteriores, limitada a [0.8, 1.25]).</summary>
    public decimal AnnualTrend { get; init; } = 1m;

    public IReadOnlyList<MonthlyProjection> Projections { get; init; } = [];
    public decimal ProjectedDemand { get; init; }
    public decimal NextMonthDemand { get; init; }
    public decimal NextMonthSeasonalIndex { get; init; }
    public decimal AverageHorizonIndex { get; init; }
    public decimal? CoverageDays { get; init; }

    /// <summary>Demanda projetada entre hoje e hoje + (lead time + 30 dias).</summary>
    public decimal DemandDuringLeadTimeWindow { get; init; }
    public decimal SafetyStock { get; init; }

    /// <summary>Necessidade de compra arredondada para cima (0 = sem compra).</summary>
    public decimal PurchaseNeed { get; init; }

    public int? DaysUntilValidity { get; init; }
    public decimal? DemandUntilValidity { get; init; }

    /// <summary>Desconto máximo (%) que preserva 10% de margem sobre o custo.</summary>
    public decimal MaxDiscountPercent { get; init; }

    /// <summary>Promoção permitida: sem promoção vigente, sem necessidade de compra e desconto máximo ≥ 5%.</summary>
    public bool CanPromote { get; init; }

    public PromotionTrigger PromotionTrigger { get; init; }

    // ---- sugestão calculada
    public RecommendationAction Action { get; init; } = RecommendationAction.Hold;
    public RecommendationPriority Priority { get; init; } = RecommendationPriority.Low;
    public decimal? SuggestedQuantity { get; init; }
    public decimal? SuggestedDiscountPercent { get; init; }
    public int? SuggestedPromotionDays { get; init; }
    public DateOnly? Deadline { get; init; }

    /// <summary>Motivo em pt-BR gerado por template (usado sem IA ou quando a IA não cobre o produto).</summary>
    public string Reason { get; internal set; } = string.Empty;

    /// <summary>Faturamento dos últimos 12 meses (para priorizar o envio à IA).</summary>
    public decimal RevenueLast12Months { get; set; }
}

/// <summary>Formatação pt-BR sem depender de dados de cultura do SO (funciona com globalização invariante).</summary>
internal static class PtBr
{
    private static readonly NumberFormatInfo Format = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3]
    };

    private static readonly string[] MonthNames =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    public static string Number(decimal value, int decimals = 0)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("N" + decimals, Format);

    /// <summary>Número com até <paramref name="maxDecimals"/> casas, sem zeros à direita (ex.: 1,2 / 3).</summary>
    public static string Compact(decimal value, int maxDecimals = 1)
    {
        var rounded = Math.Round(value, maxDecimals, MidpointRounding.AwayFromZero);
        return rounded == Math.Truncate(rounded) ? Number(rounded, 0) : Number(rounded, maxDecimals);
    }

    public static string Percent(decimal percent) => Compact(percent, 1) + "%";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string ShortDate(DateOnly date) => date.ToString("dd/MM", CultureInfo.InvariantCulture);

    public static string MonthName(int month) => MonthNames[month - 1];

    public static string MonthYear(YearMonth month) => $"{MonthNames[month.Month - 1]}/{month.Year}";

    public static string Unit(UnitType unitType) => unitType switch
    {
        UnitType.Kilogram => "kg",
        UnitType.Liter => "L",
        UnitType.Meter => "m",
        _ => "un."
    };
}
