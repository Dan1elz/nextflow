using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Card "Recomendações" do painel (T-DF1): a última análise (nula se nunca rodou) e as pendentes mais prioritárias.</summary>
public class ForecastTopDto
{
    public ForecastRunSummaryDto? Run { get; set; }
    public List<ForecastRecommendationListItemDto> Recommendations { get; set; } = [];
}
