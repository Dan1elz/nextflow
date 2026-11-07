namespace Nextflow.Domain.Dtos.Orders;

/// <summary>
/// Prévia da venda rápida (T-VD2): o carrinho como a API cobraria agora, com falta pagar e troco. Nada é gravado; o
/// rodapé da tela mostra estes valores em vez de calcular.
/// </summary>
public class QuickSalePreviewDto
{
    public List<QuickSalePreviewItemDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    /// <summary>Total − pago, nunca negativo.</summary>
    public decimal Remaining { get; set; }
    /// <summary>Recebido em dinheiro − pago em dinheiro; nulo sem valor recebido.</summary>
    public decimal? Change { get; set; }
}
