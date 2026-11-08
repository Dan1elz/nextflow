namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Contagem das recomendações de uma análise por ação e por status (T-DF2, T-DF9).</summary>
public class ForecastRecommendationCountsDto
{
    public int Buy { get; set; }
    public int Promote { get; set; }
    public int Transfer { get; set; }
    public int Hold { get; set; }
    public int Pending { get; set; }
    public int Applied { get; set; }
    public int Dismissed { get; set; }
    public int Expired { get; set; }
}
