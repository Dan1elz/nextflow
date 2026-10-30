namespace Nextflow.Domain.Utils;

/// <summary>
/// Utilitários de parse, validação e formatação para CPF e CNPJ brasileiros.
/// </summary>
public static class BrazilianDocument
{
    private static readonly int[] CnpjWeights1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
    private static readonly int[] CnpjWeights2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

    /// <summary>
    /// Normaliza um CPF ou CNPJ digitado em qualquer formato
    /// (ex: "123.456.789-09", "12.345.678/0001-95", "12345678909")
    /// para somente dígitos, validando os dígitos verificadores.
    /// </summary>
    /// <param name="input">Documento bruto digitado pelo usuário.</param>
    /// <param name="normalized">Somente dígitos se válido (11 para CPF, 14 para CNPJ); vazio caso contrário.</param>
    /// <returns><c>true</c> se o documento for válido e normalizado com sucesso.</returns>
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var digits = new string(input.Where(char.IsDigit).ToArray());

        if (digits.Length == 11 && IsValidCpf(digits))
        {
            normalized = digits;
            return true;
        }

        if (digits.Length == 14 && IsValidCnpj(digits))
        {
            normalized = digits;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Aplica máscara de exibição em um documento somente-dígitos:
    /// CPF → "000.000.000-00", CNPJ → "00.000.000/0000-00".
    /// Valores em formato inesperado são retornados sem alteração.
    /// </summary>
    public static string Mask(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        return digits.Length switch
        {
            11 => $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}",
            14 => $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}",
            _ => value
        };
    }

    /// <summary>Valida os dígitos verificadores de um CPF (somente dígitos, 11 caracteres).</summary>
    public static bool IsValidCpf(string digits)
    {
        if (digits.Length != 11) return false;
        if (digits.All(c => c == digits[0])) return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (digits[i] - '0') * (10 - i);

        var d1 = sum * 10 % 11 % 10;
        if (d1 != digits[9] - '0') return false;

        sum = 0;
        for (var i = 0; i < 10; i++)
            sum += (digits[i] - '0') * (11 - i);

        var d2 = sum * 10 % 11 % 10;
        return d2 == digits[10] - '0';
    }

    /// <summary>Valida os dígitos verificadores de um CNPJ (somente dígitos, 14 caracteres).</summary>
    public static bool IsValidCnpj(string digits)
    {
        if (digits.Length != 14) return false;
        if (digits.All(c => c == digits[0])) return false;

        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (digits[i] - '0') * CnpjWeights1[i];

        var remainder = sum % 11;
        var d1 = remainder < 2 ? 0 : 11 - remainder;
        if (d1 != digits[12] - '0') return false;

        sum = 0;
        for (var i = 0; i < 13; i++)
            sum += (digits[i] - '0') * CnpjWeights2[i];

        remainder = sum % 11;
        var d2 = remainder < 2 ? 0 : 11 - remainder;
        return d2 == digits[13] - '0';
    }
}
