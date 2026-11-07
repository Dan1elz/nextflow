using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>Forma da curva de venda de um produto ao longo do ano.</summary>
public enum DemoDemandProfile
{
    Bar,             // ano todo, alta na Páscoa e no Natal, queda jan–fev
    BonBon,          // picos Dia das Mães, Namorados e Natal
    Truffle,         // pico Namorados, depois Natal
    ChocolateDrink,  // inverno
    CocoaPowder,     // confeitaria: Páscoa e Natal
    Candy,           // Dia das Crianças
    Junina,          // festas juninas
    Biscuit,         // estável, alta no fim do ano
    GiftBasket,      // datas de presente
    EasterEgg,       // só a janela da Páscoa
    MothersKit,      // só a janela do Dia das Mães
    ValentinesKit,   // só a janela dos Namorados
    Panettone,       // novembro e dezembro
    ChristmasHamper, // novembro e dezembro
}

/// <summary>Tendência anual do volume (crescimento composto em relação à âncora).</summary>
public enum DemoTrend { Stable, Rising, Falling }

/// <summary>
/// Demanda de um produto: perfil, volume mensal de referência da loja principal (somado nas variações), tendência e se a
/// última reposição antes da âncora "atrasou" (o produto chega ao fim do histórico com pouco saldo).
/// </summary>
public sealed record DemoDemandSpec(DemoDemandProfile Profile, double BaseMonthly, DemoTrend Trend = DemoTrend.Stable, bool LateLastOrder = false);

/// <summary>
/// Demanda esperada (un./dia) do seed histórico (F2.9): sazonalidade mensal × eventos do <see cref="CommercialCalendar"/> ×
/// dia da semana × tendência. Determinístico: a simulação sorteia a venda real com Poisson sobre esta taxa. Portado do
/// <c>DemoDemandModel</c> do código antigo (branch <c>task-17</c>).
/// </summary>
public static class DemoDemand
{
    /// <summary>Calibra o volume total do histórico.</summary>
    private const double VolumeScale = 0.8;

    private const DemoTrend Rising = DemoTrend.Rising;
    private const DemoTrend Falling = DemoTrend.Falling;
    private const DemoTrend Stable = DemoTrend.Stable;

    public static readonly IReadOnlyDictionary<string, DemoDemandSpec> ByCode = new Dictionary<string, DemoDemandSpec>
    {
        ["DP-001"] = new(DemoDemandProfile.Bar, 300),
        ["DP-002"] = new(DemoDemandProfile.Bar, 170),
        ["DP-003"] = new(DemoDemandProfile.Bar, 150, Stable, LateLastOrder: true),
        ["DP-004"] = new(DemoDemandProfile.Bar, 90, Rising),
        ["DP-005"] = new(DemoDemandProfile.Bar, 70, Rising),
        ["DP-007"] = new(DemoDemandProfile.Bar, 90),
        ["DP-008"] = new(DemoDemandProfile.BonBon, 200, Stable, LateLastOrder: true),
        ["DP-009"] = new(DemoDemandProfile.BonBon, 130),
        ["DP-011"] = new(DemoDemandProfile.BonBon, 260),
        ["DP-012"] = new(DemoDemandProfile.BonBon, 35),
        ["DP-013"] = new(DemoDemandProfile.EasterEgg, 1200),
        ["DP-014"] = new(DemoDemandProfile.EasterEgg, 420),
        ["DP-015"] = new(DemoDemandProfile.EasterEgg, 650),
        ["DP-016"] = new(DemoDemandProfile.EasterEgg, 800),
        ["DP-017"] = new(DemoDemandProfile.Truffle, 480, Stable, LateLastOrder: true),
        ["DP-020"] = new(DemoDemandProfile.Truffle, 45),
        ["DP-021"] = new(DemoDemandProfile.Panettone, 340, Stable, LateLastOrder: true),
        ["DP-022"] = new(DemoDemandProfile.Panettone, 200),
        ["DP-023"] = new(DemoDemandProfile.Panettone, 180, Falling),
        ["DP-024"] = new(DemoDemandProfile.Panettone, 260),
        ["DP-025"] = new(DemoDemandProfile.ChocolateDrink, 250),
        ["DP-026"] = new(DemoDemandProfile.CocoaPowder, 110),
        ["DP-027"] = new(DemoDemandProfile.ChocolateDrink, 100),
        ["DP-028"] = new(DemoDemandProfile.CocoaPowder, 55, Rising),
        ["DP-030"] = new(DemoDemandProfile.Candy, 130),
        ["DP-031"] = new(DemoDemandProfile.Candy, 90, Falling),
        ["DP-033"] = new(DemoDemandProfile.Candy, 120),
        ["DP-034"] = new(DemoDemandProfile.Candy, 70),
        ["DP-035"] = new(DemoDemandProfile.Junina, 90),
        ["DP-036"] = new(DemoDemandProfile.Junina, 60),
        ["DP-038"] = new(DemoDemandProfile.Junina, 65),
        ["DP-039"] = new(DemoDemandProfile.Biscuit, 210),
        ["DP-040"] = new(DemoDemandProfile.Biscuit, 90, Rising),
        ["DP-041"] = new(DemoDemandProfile.Biscuit, 40, Stable, LateLastOrder: true),
        ["DP-042"] = new(DemoDemandProfile.Biscuit, 180),
        ["DP-043"] = new(DemoDemandProfile.GiftBasket, 25),
        ["DP-044"] = new(DemoDemandProfile.MothersKit, 260),
        ["DP-045"] = new(DemoDemandProfile.ValentinesKit, 220),
        ["DP-046"] = new(DemoDemandProfile.ChristmasHamper, 70, Stable, LateLastOrder: true),
        ["DP-047"] = new(DemoDemandProfile.GiftBasket, 40),
        ["DP-049"] = new(DemoDemandProfile.GiftBasket, 40),
        ["DP-050"] = new(DemoDemandProfile.GiftBasket, 45),
    };

    /// <summary>Peso de cada opção da grade na venda do produto (a do meio vende mais).</summary>
    public static double GridWeight(int optionCount, int index) => optionCount switch
    {
        <= 1 => 1,
        2 => index == 0 ? 0.6 : 0.4,
        3 => index switch { 0 => 0.35, 1 => 0.40, _ => 0.25 },
        _ => 1.0 / optionCount,
    };

    /// <summary>Unidades por dia esperadas do produto inteiro na loja principal.</summary>
    public static double DailyRate(DemoDemandSpec spec, DateOnly date, DateOnly anchor)
    {
        var years = (date.DayNumber - anchor.DayNumber) / 365.0;
        var trend = Math.Pow(spec.Trend switch { DemoTrend.Rising => 1.45, DemoTrend.Falling => 0.70, _ => 1.08 }, years);
        var weekday = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => 0.55,
            DayOfWeek.Monday => 0.90,
            DayOfWeek.Saturday => 1.35,
            _ => 1.0,
        };
        var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

        var rate = spec.Profile switch
        {
            DemoDemandProfile.EasterEgg => SeasonWindowRate(spec.BaseMonthly, date, CommercialCalendar.Easter(date.Year), 42),
            DemoDemandProfile.MothersKit => SeasonWindowRate(spec.BaseMonthly, date, CommercialCalendar.MothersDay(date.Year), 21),
            DemoDemandProfile.ValentinesKit => SeasonWindowRate(spec.BaseMonthly, date, CommercialCalendar.ValentinesDay(date.Year), 21),
            DemoDemandProfile.Panettone or DemoDemandProfile.ChristmasHamper
                => spec.BaseMonthly * ChristmasSeasonFactor(date) / daysInMonth * weekday,
            _ => spec.BaseMonthly * MonthFactor(spec.Profile, date.Month) * EventFactor(spec.Profile, date) / daysInMonth * weekday,
        };
        return rate * trend * VolumeScale;
    }

    private static double MonthFactor(DemoDemandProfile profile, int month)
    {
        double[] factors = profile switch
        {
            DemoDemandProfile.Bar => [0.80, 0.82, 1.00, 1.00, 0.98, 0.98, 1.02, 0.98, 0.98, 1.00, 1.06, 1.30],
            DemoDemandProfile.BonBon => [0.80, 0.85, 0.90, 0.95, 1.00, 1.00, 0.90, 0.90, 0.90, 0.95, 1.10, 1.00],
            DemoDemandProfile.Truffle => [0.85, 0.90, 0.95, 0.95, 1.00, 1.00, 0.90, 0.90, 0.90, 0.95, 1.00, 1.00],
            DemoDemandProfile.ChocolateDrink => [0.62, 0.62, 0.78, 0.95, 1.15, 1.38, 1.48, 1.35, 1.10, 0.90, 0.70, 0.62],
            DemoDemandProfile.CocoaPowder => [0.85, 0.85, 1.20, 1.05, 1.00, 1.10, 1.15, 1.10, 0.95, 0.95, 1.05, 1.35],
            DemoDemandProfile.Candy => [1.05, 0.95, 1.00, 1.05, 0.95, 0.95, 1.10, 0.95, 0.95, 1.20, 0.95, 1.10],
            DemoDemandProfile.Junina => [0.40, 0.40, 0.45, 0.60, 1.40, 3.20, 1.80, 0.70, 0.50, 0.45, 0.45, 0.50],
            DemoDemandProfile.Biscuit => [0.90, 0.90, 1.00, 1.00, 1.05, 1.10, 1.15, 1.05, 0.95, 0.95, 1.05, 1.30],
            DemoDemandProfile.GiftBasket => [0.60, 0.70, 0.80, 0.90, 1.00, 1.00, 0.80, 1.00, 0.80, 0.85, 1.00, 1.20],
            _ => [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        };
        return factors[month - 1];
    }

    private static double EventFactor(DemoDemandProfile profile, DateOnly date)
    {
        var easter = CommercialCalendar.Easter(date.Year);
        var mothersDay = CommercialCalendar.MothersDay(date.Year);
        var valentines = CommercialCalendar.ValentinesDay(date.Year);
        var fathersDay = CommercialCalendar.FathersDay(date.Year);
        var childrensDay = CommercialCalendar.ChildrensDay(date.Year);
        var christmas = CommercialCalendar.Christmas(date.Year);

        bool Within(DateOnly eventDate, int daysBefore) => date >= eventDate.AddDays(-daysBefore) && date <= eventDate;
        var afterChristmas = date.Month == 12 && date.Day > 25;

        return profile switch
        {
            DemoDemandProfile.Bar => Within(easter, 21) ? 1.35 : Within(childrensDay, 7) ? 1.20 : 1.0,
            DemoDemandProfile.BonBon =>
                Within(mothersDay, 14) ? 2.2 : Within(valentines, 10) ? 2.4 : Within(christmas, 24) ? 2.0 : afterChristmas ? 0.6 : 1.0,
            DemoDemandProfile.Truffle =>
                Within(valentines, 10) ? 2.5 : Within(christmas, 24) ? 1.7 : Within(mothersDay, 14) ? 1.3 : afterChristmas ? 0.7 : 1.0,
            DemoDemandProfile.CocoaPowder => Within(easter, 21) ? 1.3 : 1.0,
            DemoDemandProfile.Candy => Within(childrensDay, 14) ? 2.6 : Within(easter, 14) ? 1.3 : 1.0,
            DemoDemandProfile.GiftBasket =>
                Within(mothersDay, 14) ? 3.0 : Within(valentines, 10) ? 2.8 : Within(fathersDay, 10) ? 1.8
                : Within(christmas, 24) ? 3.2 : afterChristmas ? 0.4 : 1.0,
            _ => 1.0,
        };
    }

    /// <summary>Produto de data única: vende só na janela antes da data, crescendo até a véspera.</summary>
    private static double SeasonWindowRate(double seasonTotal, DateOnly date, DateOnly eventDate, int windowDays)
    {
        var start = eventDate.AddDays(-windowDays);
        if (date < start || date > eventDate)
            return 0;

        double Weight(int t) => 0.3 + 2.7 * Math.Pow(t / (double)windowDays, 2);

        var totalWeight = 0.0;
        for (var t = 0; t <= windowDays; t++)
            totalWeight += Weight(t);

        var dayIndex = date.DayNumber - start.DayNumber;
        var weight = date == eventDate ? Weight(dayIndex) * 0.2 : Weight(dayIndex);
        return seasonTotal * weight / totalWeight;
    }

    /// <summary>Panetones e cestas de Natal: novembro crescendo e dezembro com pico até a véspera.</summary>
    private static double ChristmasSeasonFactor(DateOnly date) => date.Month switch
    {
        11 => 0.4 + 0.9 * (date.Day / 30.0),
        12 when date.Day <= 24 => 2.0,
        12 => 0.4,
        _ => 0,
    };
}
