namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Lote de um item recebido (T-CP4). Código único por variação; código já existente com a mesma validade reaproveita o lote (F1.7).</summary>
public class ReceivePurchaseLotDto
{
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
}
