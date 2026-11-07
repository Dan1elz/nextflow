namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Lote recebido de um item (T-CP3). Lido das entradas do livro-razão do recebimento (F1.7).</summary>
public class PurchaseItemLotDto
{
    public string LotCode { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
}
