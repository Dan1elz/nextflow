namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Resultado do "Comprar" (T-DF4): os pedidos em Orçamento e as recomendações já Aplicadas, com o link.</summary>
public class ApplyPurchaseResultDto
{
    public List<AppliedPurchaseOrderDto> PurchaseOrders { get; set; } = [];
    public List<ForecastRecommendationListItemDto> Recommendations { get; set; } = [];
}
