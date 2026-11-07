namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Origem do pedido gerado pela análise (T-CP3). Preenchido a partir da F3.4.</summary>
public class PurchaseOrderForecastDto
{
    public Guid RunId { get; set; }
    public Guid RecommendationId { get; set; }
    public DateTime RunCreatedAt { get; set; }
}
