namespace Nextflow.Domain.Utils;

/// <summary>Evento comercial relevante para a demanda (datas comemorativas do varejo brasileiro).</summary>
public sealed record CommercialEvent(string Name, DateOnly Date);

/// <summary>
/// Calendário comercial calculado por ano (sem tabela fixa): Páscoa (algoritmo de Meeus/Jones/Butcher),
/// Dia das Mães, Dia dos Namorados, Dia dos Pais, Dia das Crianças, Black Friday e Natal.
/// </summary>
public static class CommercialCalendar
{
    public const string EasterName = "Páscoa";
    public const string MothersDayName = "Dia das Mães";
    public const string ValentinesDayName = "Dia dos Namorados";
    public const string FathersDayName = "Dia dos Pais";
    public const string ChildrensDayName = "Dia das Crianças";
    public const string BlackFridayName = "Black Friday";
    public const string ChristmasName = "Natal";

    /// <summary>Domingo de Páscoa (calendário gregoriano) — algoritmo de Gauss na forma de Meeus/Jones/Butcher.</summary>
    public static DateOnly Easter(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }

    /// <summary>Dia das Mães: 2º domingo de maio.</summary>
    public static DateOnly MothersDay(int year) => NthWeekdayOfMonth(year, 5, DayOfWeek.Sunday, 2);

    /// <summary>Dia dos Namorados: 12 de junho.</summary>
    public static DateOnly ValentinesDay(int year) => new(year, 6, 12);

    /// <summary>Dia dos Pais: 2º domingo de agosto.</summary>
    public static DateOnly FathersDay(int year) => NthWeekdayOfMonth(year, 8, DayOfWeek.Sunday, 2);

    /// <summary>Dia das Crianças: 12 de outubro.</summary>
    public static DateOnly ChildrensDay(int year) => new(year, 10, 12);

    /// <summary>Black Friday: sexta-feira seguinte à 4ª quinta-feira de novembro.</summary>
    public static DateOnly BlackFriday(int year) => NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4).AddDays(1);

    /// <summary>Natal: 25 de dezembro.</summary>
    public static DateOnly Christmas(int year) => new(year, 12, 25);

    /// <summary>Todos os eventos comerciais do ano, em ordem cronológica.</summary>
    public static IReadOnlyList<CommercialEvent> GetEvents(int year) =>
    [
        new(EasterName, Easter(year)),
        new(MothersDayName, MothersDay(year)),
        new(ValentinesDayName, ValentinesDay(year)),
        new(FathersDayName, FathersDay(year)),
        new(ChildrensDayName, ChildrensDay(year)),
        new(BlackFridayName, BlackFriday(year)),
        new(ChristmasName, Christmas(year)),
    ];

    /// <summary>Eventos cuja data está no intervalo [start, end] (inclusive), em ordem cronológica.</summary>
    public static IReadOnlyList<CommercialEvent> GetEventsBetween(DateOnly start, DateOnly end)
    {
        if (end < start)
            return [];

        var events = new List<CommercialEvent>();
        for (var year = start.Year; year <= end.Year; year++)
            events.AddRange(GetEvents(year).Where(e => e.Date >= start && e.Date <= end));

        return [.. events.OrderBy(e => e.Date)];
    }

    private static DateOnly NthWeekdayOfMonth(int year, int month, DayOfWeek dayOfWeek, int n)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (n - 1));
    }
}
