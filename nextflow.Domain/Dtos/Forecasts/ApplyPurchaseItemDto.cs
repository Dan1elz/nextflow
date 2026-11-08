namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Linha por variação (rateio editável, RN-DF21) com o custo unitário do pedido.</summary>
public class ApplyPurchaseItemDto
{
    public Guid RecommendationId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
