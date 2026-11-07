using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Demanda esperada (un./dia) de um produto numa data: sazonalidade mensal × eventos comerciais
/// (datas reais do <see cref="CommercialCalendar"/>) × dia da semana × tendência do cenário.
/// Determinístico: a simulação sorteia a demanda real com Poisson em cima desta taxa.
/// </summary>
internal static class DemoDemandModel
{
    // Calibra o volume total: ~20 mil pedidos em 36 meses com o catálogo atual
    private const double VolumeScale = 0.8;

    public static double DailyRate(DemoProduct product, DateOnly date, DateOnly anchor)
        => RawDailyRate(product, date, anchor) * VolumeScale;

    private static double RawDailyRate(DemoProduct product, DateOnly date, DateOnly anchor)
    {
        var years = (date.DayNumber - anchor.DayNumber) / 365.0;
        var trend = Math.Pow(AnnualGrowth(product.Scenario), years);
        var weekday = date.DayOfWeek switch
        {
            DayOfWeek.Sunday => 0.55,
            DayOfWeek.Monday => 0.90,
            DayOfWeek.Saturday => 1.35,
            _ => 1.0
        };

        return product.Profile switch
        {
            DemandProfile.EasterEgg => SeasonWindowRate(product.BaseMonthly, date, CommercialCalendar.Easter(date.Year), 42) * trend,
            DemandProfile.MothersKit => SeasonWindowRate(product.BaseMonthly, date, CommercialCalendar.MothersDay(date.Year), 21) * trend,
            DemandProfile.ValentinesKit => SeasonWindowRate(product.BaseMonthly, date, CommercialCalendar.ValentinesDay(date.Year), 21) * trend,
            DemandProfile.Panettone or DemandProfile.ChristmasHamper =>
                product.BaseMonthly * ChristmasSeasonFactor(date) / DateTime.DaysInMonth(date.Year, date.Month) * trend * weekday,
            _ => product.BaseMonthly * MonthFactor(product.Profile, date.Month) * EventFactor(product.Profile, date)
                 / DateTime.DaysInMonth(date.Year, date.Month) * trend * weekday
        };
    }

    private static double AnnualGrowth(DemoScenario scenario) => scenario switch
    {
        DemoScenario.Rising => 1.60,
        DemoScenario.Falling => 0.65,
        _ => 1.08
    };

    private static double MonthFactor(DemandProfile profile, int month)
    {
        double[] factors = profile switch
        {
            DemandProfile.Bar => [0.80, 0.82, 1.00, 1.00, 0.98, 0.98, 1.02, 0.98, 0.98, 1.00, 1.06, 1.30],
            DemandProfile.BonBon => [0.80, 0.85, 0.90, 0.95, 1.00, 1.00, 0.90, 0.90, 0.90, 0.95, 1.10, 1.00],
            DemandProfile.Truffle => [0.85, 0.90, 0.95, 0.95, 1.00, 1.00, 0.90, 0.90, 0.90, 0.95, 1.00, 1.00],
            DemandProfile.ChocolateDrink => [0.62, 0.62, 0.78, 0.95, 1.15, 1.38, 1.48, 1.35, 1.10, 0.90, 0.70, 0.62],
            DemandProfile.CocoaPowder => [0.85, 0.85, 1.20, 1.05, 1.00, 1.10, 1.15, 1.10, 0.95, 0.95, 1.05, 1.35],
            DemandProfile.Candy => [1.05, 0.95, 1.00, 1.05, 0.95, 0.95, 1.10, 0.95, 0.95, 1.20, 0.95, 1.10],
            DemandProfile.Junina => [0.40, 0.40, 0.45, 0.60, 1.40, 3.20, 1.80, 0.70, 0.50, 0.45, 0.45, 0.50],
            DemandProfile.Biscuit => [0.90, 0.90, 1.00, 1.00, 1.05, 1.10, 1.15, 1.05, 0.95, 0.95, 1.05, 1.30],
            DemandProfile.GiftBasket => [0.60, 0.70, 0.80, 0.90, 1.00, 1.00, 0.80, 1.00, 0.80, 0.85, 1.00, 1.20],
            _ => [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1]
        };
        return factors[month - 1];
    }

    private static double EventFactor(DemandProfile profile, DateOnly date)
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
            DemandProfile.Bar =>
                Within(easter, 21) ? 1.35 :
                Within(childrensDay, 7) ? 1.20 : 1.0,
            DemandProfile.BonBon =>
                Within(mothersDay, 14) ? 2.2 :
                Within(valentines, 10) ? 2.4 :
                Within(christmas, 24) ? 2.0 :
                afterChristmas ? 0.6 : 1.0,
            DemandProfile.Truffle =>
                Within(valentines, 10) ? 2.5 :
                Within(christmas, 24) ? 1.7 :
                Within(mothersDay, 14) ? 1.3 :
                afterChristmas ? 0.7 : 1.0,
            DemandProfile.CocoaPowder =>
                Within(easter, 21) ? 1.3 : 1.0,
            DemandProfile.Candy =>
                Within(childrensDay, 14) ? 2.6 :
                Within(easter, 14) ? 1.3 : 1.0,
            DemandProfile.GiftBasket =>
                Within(mothersDay, 14) ? 3.0 :
                Within(valentines, 10) ? 2.8 :
                Within(fathersDay, 10) ? 1.8 :
                Within(christmas, 24) ? 3.2 :
                afterChristmas ? 0.4 : 1.0,
            _ => 1.0
        };
    }

    /// <summary>Produto de data única (Páscoa, Dia das Mães, Namorados): vende só na janela antes da data, crescendo até a véspera.</summary>
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

    /// <summary>Panetones e cestas de Natal: só novembro (crescendo) e dezembro (pico até a véspera).</summary>
    private static double ChristmasSeasonFactor(DateOnly date) => date.Month switch
    {
        11 => 0.4 + 0.9 * (date.Day / 30.0),
        12 when date.Day <= 24 => 2.0,
        12 => 0.4,
        _ => 0
    };
}
