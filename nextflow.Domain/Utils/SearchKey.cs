using System.Globalization;
using System.Text;

namespace Nextflow.Domain.Utils;

public static class SearchKey
{
    /// <summary>
    /// Só letras e dígitos, sem acento e em minúsculas: "Santa Bárbara d'Oeste" vira "santabarbaradoeste". Assim
    /// "santa barbara", "Santa Bárbara D'Oeste" e "santa barbara d oeste" acham a mesma cidade.
    /// </summary>
    public static string From(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || !char.IsLetterOrDigit(c))
                continue;
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
