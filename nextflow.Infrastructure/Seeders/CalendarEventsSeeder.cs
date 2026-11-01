using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Os sete eventos nacionais (RN-CE47), em todo ambiente. Só acrescenta o que falta, comparando pelo nome como RN-CE44:
/// nunca sobrescreve <c>lead_days</c> nem <c>is_active</c> de quem já existe, para não desfazer o que o Admin ajustou.
/// </summary>
public class CalendarEventsSeeder(ICalendarEventRepository calendarEventRepository, IUnitOfWork unitOfWork) : ISeeder
{
    /// <summary>Antecedência: a maior janela de venda antes da data em <c>DemoDemandModel</c>; a Black Friday não tem e usa o padrão.</summary>
    private static readonly CalendarEventRequestDto[] NationalEvents =
    [
        new() { Name = CommercialCalendar.EasterName, DateRule = DateRuleEnum.Easter, LeadDays = 42 },
        SecondSunday(CommercialCalendar.MothersDayName, month: 5, leadDays: 21),
        Fixed(CommercialCalendar.ValentinesDayName, month: 6, day: 12, leadDays: 21),
        SecondSunday(CommercialCalendar.FathersDayName, month: 8, leadDays: 10),
        Fixed(CommercialCalendar.ChildrensDayName, month: 10, day: 12, leadDays: 14),
        new() { Name = CommercialCalendar.BlackFridayName, DateRule = DateRuleEnum.BlackFriday, LeadDays = 15 },
        Fixed(CommercialCalendar.ChristmasName, month: 12, day: 25, leadDays: 54),
    ];

    public int Order => 30;

    public async Task SeedAsync(CancellationToken ct)
    {
        var added = false;

        foreach (var dto in NationalEvents)
        {
            var name = dto.Name.ToLowerInvariant();
            if (await calendarEventRepository.Exists(e => e.Name.ToLower() == name, ct))
                continue;

            await calendarEventRepository.AddAsync(new CalendarEvent(dto, EventScopeEnum.National), ct);
            added = true;
        }

        if (added)
            await unitOfWork.CommitAsync(ct);
    }

    private static CalendarEventRequestDto Fixed(string name, int month, int day, int leadDays)
        => new() { Name = name, DateRule = DateRuleEnum.Fixed, Month = month, Day = day, LeadDays = leadDays };

    private static CalendarEventRequestDto SecondSunday(string name, int month, int leadDays) => new()
    {
        Name = name,
        DateRule = DateRuleEnum.NthWeekday,
        Month = month,
        NthWeekday = new NthWeekdayRequestDto { Weekday = (int)DayOfWeek.Sunday, Nth = 2 },
        LeadDays = leadDays,
    };
}
