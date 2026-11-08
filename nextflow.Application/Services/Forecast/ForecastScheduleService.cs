using Microsoft.Extensions.Logging;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Agendamento semanal (RN-DF18, T-DF11). Não recupera horário perdido com a API desligada (vale o da semana seguinte); se houver
/// análise em andamento no horário, devolve o mesmo ponto e a próxima conferência tenta de novo.
/// </summary>
public class ForecastScheduleService(
    IForecastSettingsRepository settingsRepository,
    IForecastRunLauncher launcher,
    IBusinessClock clock,
    ILogger<ForecastScheduleService> logger) : IForecastScheduleService
{
    public async Task<DateTime> CheckAsync(DateTime handledUntilLocal, CancellationToken ct)
    {
        var settings = await settingsRepository.Get(_ => true, ct: ct) ?? new ForecastSettings();
        var now = clock.Now;
        if (!settings.ScheduleEnabled)
            return now;

        var slot = ForecastSchedule.LastSlot(now, settings.Weekday, settings.Time);
        if (slot <= handledUntilLocal)
            return handledUntilLocal;

        var run = await launcher.StartAsync(ForecastTriggerEnum.Scheduled, null, ct);
        if (run is null)
        {
            logger.LogInformation("Previsão: horário agendado de {Slot:g} aguardando a análise em andamento terminar.", slot);
            return handledUntilLocal;
        }

        return now;
    }
}
