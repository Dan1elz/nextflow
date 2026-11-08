namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Resultado do "Promover" (T-DF6): as promoções criadas e a recomendação Aplicada.</summary>
public class ApplyPromotionResultDto
{
    public List<Guid> PromotionIds { get; set; } = [];
    public ForecastRecommendationListItemDto Recommendation { get; set; } = new();
}
