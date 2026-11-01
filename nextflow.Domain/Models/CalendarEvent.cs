using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Models;

/// <summary>Evento do calendário comercial: nacional (seed) ou da empresa (RN-CE22, RN-CE41 a RN-CE47).</summary>
public class CalendarEvent : BaseModel, IDeletable, IUpdatable<CalendarEventRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public EventScopeEnum Scope { get; private set; }
    public DateRuleEnum DateRule { get; private set; }
    public short? Month { get; private set; }
    public short? Day { get; private set; }
    public short? Weekday { get; private set; }
    public short? Nth { get; private set; }
    public short? EasterOffsetDays { get; private set; }
    public short LeadDays { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "evento";
    public override string Plural => "eventos";

    private CalendarEvent() { }

    public CalendarEvent(CalendarEventRequestDto dto, EventScopeEnum scope)
    {
        Scope = scope;
        Apply(dto);
    }

    /// <summary>Evento nacional só muda a antecedência (RN-CE42); o use case recusa antes o que mudaria o resto.</summary>
    public void Update(CalendarEventRequestDto dto)
    {
        if (Scope == EventScopeEnum.National)
            LeadDays = (short)dto.LeadDays.GetValueOrDefault();
        else
            Apply(dto);

        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>O DTO mantém o nome e a regra de data gravados? Campos fora da regra não contam, pois viram nulo (RN-CE41).</summary>
    public bool HasSameDefinition(CalendarEventRequestDto dto)
    {
        var other = new CalendarEvent(dto, Scope);

        return Name == other.Name
            && DateRule == other.DateRule
            && Month == other.Month
            && Day == other.Day
            && Weekday == other.Weekday
            && Nth == other.Nth
            && EasterOffsetDays == other.EasterOffsetDays;
    }

    /// <summary>Data do evento no ano. Toda regra cai dentro do próprio ano (RN-CE41).</summary>
    public DateOnly DateIn(int year) => DateRule switch
    {
        DateRuleEnum.Fixed => new DateOnly(year, Month.GetValueOrDefault(), Day.GetValueOrDefault()),
        DateRuleEnum.Easter => CommercialCalendar.Easter(year),
        DateRuleEnum.NthWeekday => CommercialCalendar.NthWeekdayOfMonth(
            year, Month.GetValueOrDefault(), (DayOfWeek)Weekday.GetValueOrDefault(), Nth.GetValueOrDefault()),
        DateRuleEnum.BlackFriday => CommercialCalendar.BlackFriday(year),
        DateRuleEnum.EasterOffset => CommercialCalendar.Easter(year).AddDays(EasterOffsetDays.GetValueOrDefault()),
        _ => throw new InvalidOperationException($"Regra de data desconhecida: {DateRule}."),
    };

    /// <summary>Próxima ocorrência em que a data é hoje ou depois: hoje ainda conta (RN-CE46).</summary>
    public DateOnly NextDate(DateOnly today)
    {
        var date = DateIn(today.Year);
        return date >= today ? date : DateIn(today.Year + 1);
    }

    private void Apply(CalendarEventRequestDto dto)
    {
        Name = dto.Name.Trim();
        DateRule = dto.DateRule;
        LeadDays = (short)dto.LeadDays.GetValueOrDefault();

        // RN-CE41: o que não pertence à regra é gravado como nulo.
        var isNthWeekday = dto.DateRule == DateRuleEnum.NthWeekday;
        Month = dto.DateRule is DateRuleEnum.Fixed or DateRuleEnum.NthWeekday ? (short?)dto.Month : null;
        Day = dto.DateRule == DateRuleEnum.Fixed ? (short?)dto.Day : null;
        Weekday = isNthWeekday ? (short?)dto.NthWeekday?.Weekday : null;
        Nth = isNthWeekday ? (short?)dto.NthWeekday?.Nth : null;
        EasterOffsetDays = dto.DateRule == DateRuleEnum.EasterOffset ? (short?)dto.EasterOffsetDays : null;
    }
}
