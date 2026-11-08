namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Pedido de compra gerado pelo "Comprar" (T-DF4).</summary>
public class AppliedPurchaseOrderDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal Total { get; set; }
}
