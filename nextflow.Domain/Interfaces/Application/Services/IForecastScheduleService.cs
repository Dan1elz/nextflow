namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Conferência do agendamento semanal (RN-DF18), chamada a cada minuto pelo job: se passou um horário agendado depois de
/// <c>handledUntilLocal</c> e o agendamento está ligado, abre uma análise <c>Scheduled</c>.
/// </summary>
public interface IForecastScheduleService
{
    /// <summary>Devolve até onde (horário local) o agendamento já foi tratado.</summary>
    Task<DateTime> CheckAsync(DateTime handledUntilLocal, CancellationToken ct);
}
