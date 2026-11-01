using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.CalendarEvents;

public class NthWeekdayResponseDto
{
    public int Weekday { get; set; }
    public int Nth { get; set; }

    public NthWeekdayResponseDto() { }

    public NthWeekdayResponseDto(CalendarEvent entity)
    {
        Weekday = entity.Weekday.GetValueOrDefault();
        Nth = entity.Nth.GetValueOrDefault();
    }
}
