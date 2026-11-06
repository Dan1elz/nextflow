namespace Nextflow.Domain.Dtos.Sales;

/// <summary>Lote que a venda baixaria por FEFO (T-VD1, RN-CE15). Preenchido a partir da F1.7; até lá vem nulo.</summary>
public class SaleOptionLotDto
{
    public Guid LotId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
}
