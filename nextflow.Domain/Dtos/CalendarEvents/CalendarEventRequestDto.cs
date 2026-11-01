using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.CalendarEvents;

/// <summary>Evento de calendário (T6). O escopo nunca vem do cliente (RN-CE42).</summary>
public class CalendarEventRequestDto
{
    public string Name { get; set; } = string.Empty;
    public DateRuleEnum DateRule { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public NthWeekdayRequestDto? NthWeekday { get; set; }
    public int? EasterOffsetDays { get; set; }
    public int? LeadDays { get; set; }
}
