namespace Nextflow.Domain.Dtos.Products;

/// <summary>Linha da tabela de variações. <see cref="AttributeValueIds"/> traz ids de valores existentes ou <c>tempKey</c> de valores novos.</summary>
public class ProductVariantRequestDto
{
    public Guid? Id { get; set; }
    public List<string> AttributeValueIds { get; set => field = value ?? []; } = [];
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
