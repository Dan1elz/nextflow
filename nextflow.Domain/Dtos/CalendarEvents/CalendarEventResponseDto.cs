using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.CalendarEvents;

/// <summary>
/// Evento de calendário (T6). <see cref="NextDate"/> é calculado aqui, a partir da data de negócio recebida (RN-CE46).
/// <see cref="ProductCount"/> fica zero até a F1.4, que traz <c>product_events</c>.
/// </summary>
public class CalendarEventResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string DateRule { get; set; } = string.Empty;
    public int? Month { get; set; }
    public int? Day { get; set; }
    public NthWeekdayResponseDto? NthWeekday { get; set; }
    public int? EasterOffsetDays { get; set; }
    public int LeadDays { get; set; }
    public DateOnly NextDate { get; set; }
    public int ProductCount { get; set; }
    public bool IsActive { get; set; }

    public CalendarEventResponseDto() { }

    public CalendarEventResponseDto(CalendarEvent entity, DateOnly today)
    {
        Id = entity.Id;
        Name = entity.Name;
        Scope = entity.Scope.GetApiName();
        DateRule = entity.DateRule.GetApiName();
        Month = entity.Month;
        Day = entity.Day;
        NthWeekday = entity.DateRule == DateRuleEnum.NthWeekday ? new NthWeekdayResponseDto(entity) : null;
        EasterOffsetDays = entity.EasterOffsetDays;
        LeadDays = entity.LeadDays;
        NextDate = entity.NextDate(today);
        IsActive = entity.IsActive;
    }
}
