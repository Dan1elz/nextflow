namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Mínimo de uma variação num local (T2, RN-CE33). A variação é citada pela posição em <c>variants</c> do mesmo
/// formulário, porque a variação nova ainda não tem id.
/// </summary>
public class ProductLocationMinimumDto
{
    public int VariantIndex { get; set; }
    public Guid LocationId { get; set; }
    public decimal MinQuantity { get; set; }
}
