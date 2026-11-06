namespace Nextflow.Domain.Dtos.Stock;

/// <summary>
/// Saldo de uma variação num local. Enquanto não houver orçamento, reservado é zero e disponível = físico (RN-CE08).
/// <see cref="MinQuantity"/> é o mínimo efetivo: o do local ou, sem ele, o padrão do produto (RN-CE33).
/// </summary>
public class StockMatrixCellDto
{
    public Guid LocationId { get; set; }
    public decimal Physical { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
    public decimal MinQuantity { get; set; }
    public bool IsBelowMin { get; set; }

    public StockMatrixCellDto() { }

    public StockMatrixCellDto(Guid locationId, decimal physical, decimal minQuantity)
    {
        LocationId = locationId;
        Physical = physical;
        Reserved = 0;
        Available = physical;
        MinQuantity = minQuantity;
        IsBelowMin = Available < minQuantity;
    }
}
