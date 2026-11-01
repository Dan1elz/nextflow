namespace Nextflow.Domain.Dtos.CalendarEvents;

/// <summary>Enésimo dia da semana: <see cref="Weekday"/> de 0 (domingo) a 6 (sábado), <see cref="Nth"/> de 1 a 4.</summary>
public class NthWeekdayRequestDto
{
    public int? Weekday { get; set; }
    public int? Nth { get; set; }
}
