using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Abertura de uma análise (F3.3 cria; a F3.2 só executa). <c>RequestedById</c> é nulo na análise agendada (RN-DF18).</summary>
public record StartForecastRunDto(ForecastTriggerEnum Trigger, Guid? RequestedById);
