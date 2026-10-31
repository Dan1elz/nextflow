namespace Nextflow.Domain.Utils;

public static class Digits
{
    /// <summary>Só os dígitos do texto (CEP, telefone, documento digitado com máscara).</summary>
    public static string Only(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
