namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Documento que gerou a movimentação, para o link do kardex. <see cref="Number"/> entra com cada documento (F2).</summary>
public class StockMovementSourceDto
{
    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public int? Number { get; set; }
}
