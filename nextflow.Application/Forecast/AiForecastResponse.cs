namespace Nextflow.Application.Forecast;

/// <summary>Resposta da IA a um lote (spec 6.4). <c>ResumoGeral</c> só é usado na chamada final de resumo (RN-DF20).</summary>
public sealed class AiForecastResponse
{
    public string? ResumoGeral { get; set; }
    public List<AiRecommendationItem>? Recomendacoes { get; set; }
}
