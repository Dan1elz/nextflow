using Nextflow.Domain.Utils;

namespace Nextflow.Domain.ValueObjects;

/// <summary>
/// Representa um documento fiscal brasileiro válido — CPF (11 dígitos) ou CNPJ (14 dígitos).
/// Armazena somente dígitos internamente; use <see cref="Masked"/> para exibição formatada.
/// </summary>
public sealed class TaxDocument : ValueObject
{
    /// <summary>Documento normalizado contendo somente dígitos (sem pontuação).</summary>
    public string Value { get; }

    /// <summary>Verdadeiro quando o documento é um CPF (11 dígitos).</summary>
    public bool IsCpf => Value.Length == 11;

    /// <summary>Verdadeiro quando o documento é um CNPJ (14 dígitos).</summary>
    public bool IsCnpj => Value.Length == 14;

    /// <summary>
    /// Documento formatado para exibição: CPF no formato "000.000.000-00"
    /// ou CNPJ no formato "00.000.000/0000-00".
    /// </summary>
    public string Masked => BrazilianDocument.Mask(Value);

    private TaxDocument(string value) => Value = value;

    /// <summary>
    /// Normaliza e cria um <see cref="TaxDocument"/> a partir da entrada bruta (CPF ou CNPJ).
    /// </summary>
    /// <exception cref="InvalidOperationException">Lançada se o documento for inválido.</exception>
    public static TaxDocument Parse(string input)
    {
        if (!BrazilianDocument.TryNormalize(input, out var v))
            throw new InvalidOperationException("CPF/CNPJ inválido.");
        return new(v);
    }

    /// <summary>
    /// Tenta normalizar e criar um <see cref="TaxDocument"/> (CPF ou CNPJ).
    /// Retorna <c>false</c> sem lançar exceção se inválido.
    /// </summary>
    public static bool TryParse(string? input, out TaxDocument? result)
    {
        result = null;
        if (!BrazilianDocument.TryNormalize(input, out var v)) return false;
        result = new(v);
        return true;
    }

    /// <summary>
    /// Tenta normalizar e criar um <see cref="TaxDocument"/> aceitando <b>somente CPF</b>.
    /// Retorna <c>false</c> para entradas com 14 dígitos (CNPJ) ou inválidas.
    /// </summary>
    public static bool TryParseCpf(string? input, out TaxDocument? result)
    {
        result = null;
        if (!BrazilianDocument.TryNormalize(input, out var v) || v.Length != 11) return false;
        result = new(v);
        return true;
    }

    /// <summary>CPF já validado pelo FluentValidation (ex.: no construtor do modelo). Lança se não for CPF válido.</summary>
    public static TaxDocument ParseCpf(string input)
        => TryParseCpf(input, out var result) ? result! : throw new InvalidOperationException("CPF inválido.");

    /// <summary>CNPJ já validado pelo FluentValidation (ex.: no construtor do modelo). Lança se não for CNPJ válido.</summary>
    public static TaxDocument ParseCnpj(string input)
        => TryParseCnpj(input, out var result) ? result! : throw new InvalidOperationException("CNPJ inválido.");

    /// <summary>Aceita <b>somente CNPJ</b> (14 dígitos válidos).</summary>
    public static bool TryParseCnpj(string? input, out TaxDocument? result)
    {
        result = null;
        if (!BrazilianDocument.TryNormalize(input, out var v) || v.Length != 14) return false;
        result = new(v);
        return true;
    }

    /// <summary>Para validators: o texto é um CPF válido (com ou sem pontuação).</summary>
    public static bool IsValidCpf(string? input) => TryParseCpf(input, out _);

    /// <summary>Para validators: o texto é um CNPJ válido (com ou sem pontuação).</summary>
    public static bool IsValidCnpj(string? input) => TryParseCnpj(input, out _);

    /// <summary>
    /// CPF com o meio escondido para listagens e combos ("123.***.***-09"): nunca mostra o documento completo onde
    /// não precisa.
    /// </summary>
    public string MaskedPartially => IsCpf ? $"{Value[..3]}.***.***-{Value[9..]}" : Masked;

    /// <summary>
    /// Lê o valor diretamente do banco sem revalidar. Exclusivo para conversões EF.
    /// </summary>
    public static TaxDocument FromStored(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }

    public override string ToString() => Value;
    public static implicit operator string(TaxDocument d) => d.Value;
}
