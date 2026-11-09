namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Quanto comprar de uma variação num local, pedido na hora pelo estoque baixo do painel (RN-DF30, a aprovar).</summary>
public class PurchaseSuggestionDto
{
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Available { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal Incoming { get; set; }
    /// <summary>Venda média por dia da variação na loja nos últimos 90 dias.</summary>
    public decimal DailySales { get; set; }
    public int LeadTimeDays { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    /// <summary>Quantidade sugerida: a da IA dentro dos limites, ou a calculada.</summary>
    public decimal Quantity { get; set; }
    public decimal CalculatedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Verdadeiro quando a quantidade e o motivo vieram da IA.</summary>
    public bool FromAi { get; set; }
}
