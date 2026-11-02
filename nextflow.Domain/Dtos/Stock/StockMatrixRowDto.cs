namespace Nextflow.Domain.Dtos.Stock;

public class StockMatrixRowDto
{
    public Guid VariantId { get; set; }

    /// <summary>"P / Azul"; vazio na variação padrão oculta.</summary>
    public string VariantLabel { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;
    public List<StockMatrixCellDto> Cells { get; set; } = [];
}
