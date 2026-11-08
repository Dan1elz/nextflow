using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Abre uma análise (RN-DF18, RN-DF23): grava em <c>Running</c> com a data de negócio e enfileira para o job. Usado pelo botão
/// manual e pelo agendamento semanal. Devolve nulo se já houver uma análise em andamento (só uma por vez).
/// </summary>
public interface IForecastRunLauncher
{
    Task<ForecastRun?> StartAsync(ForecastTriggerEnum trigger, Guid? requestedById, CancellationToken ct = default);
}
