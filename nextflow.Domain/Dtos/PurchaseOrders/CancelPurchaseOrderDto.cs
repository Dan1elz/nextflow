namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Cancelamento (T-CP5): versão lida e motivo obrigatório.</summary>
public class CancelPurchaseOrderDto
{
    public uint Version { get; set; }
    public string Reason { get; set; } = string.Empty;
}
