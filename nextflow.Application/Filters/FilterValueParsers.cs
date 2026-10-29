using System.Globalization;
using System.Linq;

namespace Nextflow.Application.Filters;

/// <summary>
/// Provedor estático de Parsers para converter strings em diversos tipos (Guid, Bool, Int, Decimal, etc).
/// Suporta fallback de culturas para <c>pt-BR</c> e <c>InvariantCulture</c>.
/// </summary>
public static class FilterValueParsers
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Tenta converter uma string para Guid.
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="guid">O Guid resultante.</param>
    /// <returns>True se a conversão for bem sucedida.</returns>
    public static bool TryParseGuid(string value, out Guid guid)
        => Guid.TryParse(value, out guid);

    /// <summary>
    /// Tenta converter uma string para booleano.
    /// Aceita diversos padrões (1/0, yes/no, sim/nao, true/false, on/off).
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="b">O booleano resultante.</param>
    /// <returns>True se o valor for mapeável para booleano.</returns>
    public static bool TryParseBool(string value, out bool b)
    {
        var v = value.Trim().ToLowerInvariant();
        switch (v)
        {
            case "true":
            case "1":
            case "yes":
            case "y":
            case "on":
            case "sim":
            case "s":
                b = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "n":
            case "off":
            case "nao":
            case "não":
                b = false;
                return true;
            default:
                return bool.TryParse(v, out b);
        }
    }

    /// <summary>
    /// Tenta converter uma string para Inteiro. Suporta pontos e virgulas conforme cultura pt-BR.
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="i">O inteiro resultante.</param>
    /// <returns>True se a conversão for bem sucedida.</returns>
    public static bool TryParseInt(string value, out int i)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)
           || int.TryParse(value, NumberStyles.Integer, PtBr, out i);

    /// <summary>
    /// Tenta converter uma string para um enum. Aceita tanto o nome do membro em C#
    /// (PascalCase, case-insensitive) quanto o api_name em snake_case usado nas
    /// respostas da API e pelo frontend (ex.: "pending_approval" → PendingApproval).
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="result">O valor do enum resultante.</param>
    /// <returns>True se a conversão for bem sucedida.</returns>
    public static bool TryParseEnum<TEnum>(string value, out TEnum result) where TEnum : struct, Enum
    {
        if (Enum.TryParse(value, ignoreCase: true, out result))
            return true;

        var pascalCase = string.Concat(
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        return Enum.TryParse(pascalCase, ignoreCase: true, out result);
    }

    /// <summary>
    /// Tenta converter uma string para Decimal. Suporta múltiplos formatos culturais.
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="d">O decimal resultante.</param>
    /// <returns>True se a conversão for bem sucedida.</returns>
    public static bool TryParseDecimal(string value, out decimal d)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out d)
           || decimal.TryParse(value, NumberStyles.Number, PtBr, out d);

    /// <summary>
    /// Tenta converter uma string para DateOnly.
    /// Prioriza ISO (yyyy-MM-dd) seguido de BR (dd/MM/yyyy).
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="date">A data resultante.</param>
    /// <returns>True se mapeado.</returns>
    public static bool TryParseDateOnly(string value, out DateOnly date)
    {
        // Preferência: ISO-8601 (yyyy-MM-dd). Aceita também pt-BR (dd/MM/yyyy).
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (DateOnly.TryParseExact(value, "dd/MM/yyyy", PtBr, DateTimeStyles.None, out date))
            return true;

        // fallback: tentar DateTime e pegar a parte da data
        if (TryParseDateTime(value, out var dt))
        {
            date = DateOnly.FromDateTime(dt);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tenta converter uma string para DateTime (UTC preferencialmente).
    /// </summary>
    /// <param name="value">A string de entrada.</param>
    /// <param name="dt">O DateTime resultante.</param>
    /// <returns>True se a data for válida.</returns>
    public static bool TryParseDateTime(string value, out DateTime dt)
    {
        // ISO-8601 comum
        var formats = new[]
        {
            "yyyy-MM-ddTHH:mm:ss.FFFK",
            "yyyy-MM-ddTHH:mm:ssK",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dt))
            return true;

        // fallback culturas
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dt))
            return true;

        return DateTime.TryParse(value, PtBr,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out dt);
    }
}
