using System.Globalization;
using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

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

    public static string Unit(SaleUnitEnum unit) => unit switch
    {
        SaleUnitEnum.Kilogram => "kg",
        SaleUnitEnum.Liter => "L",
        SaleUnitEnum.Meter => "m",
        _ => "un."
    };
}
