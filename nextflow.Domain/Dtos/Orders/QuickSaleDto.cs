namespace Nextflow.Domain.Dtos.Orders;

/// <summary>
/// Venda rápida de balcão (T-VD2): nasce Paga numa chamada só. Sem <see cref="SaleLocationId"/>, baixa do local de venda
/// padrão da loja. Os valores dos pagamentos são líquidos; <see cref="CashReceived"/> só serve para o troco.
/// </summary>
public class QuickSaleDto
{
    public Guid StoreId { get; set; }
    public Guid? SaleLocationId { get; set; }
    public Guid? ClientId { get; set; }
    public List<QuickSaleItemDto> Items { get; set; } = [];
    public List<QuickSalePaymentDto> Payments { get; set; } = [];
    public decimal? CashReceived { get; set; }
    /// <summary>Reenvio depois do aviso de preço alterado desde a busca.</summary>
    public bool ConfirmWarnings { get; set; }
}
