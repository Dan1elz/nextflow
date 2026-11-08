namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>PUT do agendamento (T-DF11). <c>Weekday</c> 0 = domingo a 6 = sábado; <c>Time</c> "HH:mm" no fuso de negócio.</summary>
public class ForecastSettingsRequestDto
{
    public bool ScheduleEnabled { get; set; }
    public int Weekday { get; set; }
    public string Time { get; set; } = string.Empty;
    public int AiBatchSize { get; set; }
}
