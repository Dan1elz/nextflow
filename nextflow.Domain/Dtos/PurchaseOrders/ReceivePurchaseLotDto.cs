namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Lote de um item recebido (T-CP4). Recusado até a F1.7 (lotes).</summary>
public class ReceivePurchaseLotDto
{
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
}
