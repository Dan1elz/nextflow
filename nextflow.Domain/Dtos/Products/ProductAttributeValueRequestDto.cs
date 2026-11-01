namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Valor do eixo (P, Azul). Valor novo não tem <see cref="Id"/> e chega com <see cref="TempKey"/>, que a variação cita em
/// <c>attributeValueIds</c> na mesma requisição (lacuna L1, RN-CE50).
/// </summary>
public class ProductAttributeValueRequestDto
{
    public Guid? Id { get; set; }
    public string? TempKey { get; set; }
    public string Value { get; set; } = string.Empty;

    /// <summary>O que as variações citam: o id, ou a chave temporária do valor novo.</summary>
    public string? GetKey() => Id?.ToString() ?? TempKey;
}
