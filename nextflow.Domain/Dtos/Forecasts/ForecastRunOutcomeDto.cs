using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Resumo do que o motor gravou numa análise (a leitura completa é da F3.3 e da F3.5).</summary>
public record ForecastRunOutcomeDto(
    Guid RunId,
    ForecastRunStatusEnum Status,
    short HorizonMonths,
    int ProductsAnalyzed,
    int ProductsSentToAi,
    int Buy,
    int Promote,
    int Hold,
    int BatchesTotal,
    int BatchesFailed);
