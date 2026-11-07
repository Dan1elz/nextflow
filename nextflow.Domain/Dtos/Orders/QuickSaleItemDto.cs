namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Linha da venda rápida. O preço cobrado é sempre o efetivo do dia, calculado pela API (RN-VD02).</summary>
public class QuickSaleItemDto
{
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Desconto em R$ da linha inteira (RN-VD03).</summary>
    public decimal Discount { get; set; }
    /// <summary>Recusado até a F1.7 (lotes).</summary>
    public Guid? LotId { get; set; }
    /// <summary>Preço efetivo que o seletor mostrou; diferente do atual, a API pede confirmação.</summary>
    public decimal? ExpectedUnitPrice { get; set; }
}
