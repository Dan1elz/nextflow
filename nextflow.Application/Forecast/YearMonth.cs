namespace Nextflow.Application.Forecast;

/// <summary>Mês de calendário (ano + mês) com aritmética simples.</summary>
public readonly record struct YearMonth(int Year, int Month) : IComparable<YearMonth>
{
    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    /// <summary>Número sequencial do mês (para diferenças e comparações).</summary>
    public int Ordinal => Year * 12 + (Month - 1);

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DaysInMonth);

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
