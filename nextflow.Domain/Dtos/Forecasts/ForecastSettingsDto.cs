using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Agendamento da análise (T-DF11). <c>AiConfigured</c> só diz se há chave; a chave nunca sai da API.</summary>
public class ForecastSettingsDto
{
    public bool ScheduleEnabled { get; set; }
    public int Weekday { get; set; }
    public string Time { get; set; } = string.Empty;
    public int AiBatchSize { get; set; }
    public bool AiConfigured { get; set; }
    public DateTime? NextScheduledAt { get; set; }

    public ForecastSettingsDto() { }

    public ForecastSettingsDto(ForecastSettings entity, bool aiConfigured, DateTime? nextScheduledAt)
    {
        ScheduleEnabled = entity.ScheduleEnabled;
        Weekday = (int)entity.Weekday;
        Time = entity.Time.ToString("HH:mm");
        AiBatchSize = entity.AiBatchSize;
        AiConfigured = aiConfigured;
        NextScheduledAt = nextScheduledAt;
    }
}
