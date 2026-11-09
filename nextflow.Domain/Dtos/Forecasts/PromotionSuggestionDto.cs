namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Desconto e duração para escoar um lote vencendo num local, pedidos na hora pelo painel (RN-DF30, a aprovar).</summary>
public class PromotionSuggestionDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    public Guid LotId { get; set; }
    public Guid LocationId { get; set; }
    public Guid StoreId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public int DaysToExpire { get; set; }
    /// <summary>Venda média por dia da variação na loja nos últimos 90 dias.</summary>
    public decimal DailySales { get; set; }
    /// <summary>Quanto a venda normal escoa até o vencimento.</summary>
    public decimal ExpectedSales { get; set; }
    public decimal MaxDiscountPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public int PromotionDays { get; set; }
    public decimal CalculatedDiscountPercent { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool FromAi { get; set; }
}
