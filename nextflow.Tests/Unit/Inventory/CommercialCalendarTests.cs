using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Tests.Unit.Inventory;

public class CommercialCalendarTests
{
    private static CalendarEvent Event(CalendarEventRequestDto dto)
    {
        dto.Name = "Evento";
        dto.LeadDays = 10;
        return new CalendarEvent(dto, EventScopeEnum.Company);
    }

    private static CalendarEvent Fixed(int month, int day) => Event(new() { DateRule = DateRuleEnum.Fixed, Month = month, Day = day });

    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [Trait("Rule", "RN-CE41")]
    public void Should_ReturnKnownEasterSunday_When_GivenYear(int year, int month, int day)
        => CommercialCalendar.Easter(year).ShouldBe(new DateOnly(year, month, day));

    [Theory]
    [InlineData(2026, 5, 10, 8, 9)]
    [InlineData(2027, 5, 9, 8, 8)]
    [Trait("Rule", "RN-CE41")]
    public void Should_ReturnSecondSunday_When_MothersAndFathersDay(int year, int mothersMonth, int mothersDay, int fathersMonth, int fathersDay)
    {
        CommercialCalendar.MothersDay(year).ShouldBe(new DateOnly(year, mothersMonth, mothersDay));
        CommercialCalendar.FathersDay(year).ShouldBe(new DateOnly(year, fathersMonth, fathersDay));
    }

    [Theory]
    [InlineData(2024, 29)] // novembro começa numa sexta: 4ª quinta = 28
    [InlineData(2025, 28)]
    [InlineData(2026, 27)]
    [Trait("Rule", "RN-CE41")]
    public void Should_BeFridayAfterFourthThursdayOfNovember_When_BlackFriday(int year, int day)
    {
        var date = CommercialCalendar.BlackFriday(year);

        date.ShouldBe(new DateOnly(year, 11, day));
        date.DayOfWeek.ShouldBe(DayOfWeek.Friday);
    }

    [Fact]
    public void Should_ListSevenEventsInOrder_When_GettingEventsOfYear()
    {
        var events = CommercialCalendar.GetEvents(2026);

        events.Count.ShouldBe(7);
        events.ShouldBe(events.OrderBy(e => e.Date).ToList());
    }

    [Fact]
    public void Should_CrossTheYear_When_GettingEventsBetween()
    {
        var events = CommercialCalendar.GetEventsBetween(new DateOnly(2026, 12, 20), new DateOnly(2027, 4, 1));

        events.Select(e => e.Name).ShouldBe([CommercialCalendar.ChristmasName, CommercialCalendar.EasterName]);
    }

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_ApplyNegativeOffset_When_RuleIsEasterOffset()
        => Event(new() { DateRule = DateRuleEnum.EasterOffset, EasterOffsetDays = -47 }).DateIn(2026).ShouldBe(new DateOnly(2026, 2, 17));

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_FindNthWeekday_When_RuleIsNthWeekday()
    {
        var dto = new CalendarEventRequestDto
        {
            DateRule = DateRuleEnum.NthWeekday,
            Month = 3,
            NthWeekday = new NthWeekdayRequestDto { Weekday = (int)DayOfWeek.Monday, Nth = 4 },
        };

        Event(dto).DateIn(2026).ShouldBe(new DateOnly(2026, 3, 23));
    }

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public void Should_ReturnToday_When_EventIsToday()
        => Fixed(10, 12).NextDate(new DateOnly(2026, 10, 12)).ShouldBe(new DateOnly(2026, 10, 12));

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public void Should_ReturnThisYear_When_EventIsStillAhead()
        => Fixed(10, 12).NextDate(new DateOnly(2026, 10, 11)).ShouldBe(new DateOnly(2026, 10, 12));

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public void Should_RollToNextYear_When_EventHasPassed()
        => Fixed(10, 12).NextDate(new DateOnly(2026, 10, 13)).ShouldBe(new DateOnly(2027, 10, 12));

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public void Should_RecalculateEasterForNextYear_When_EasterHasPassed()
        => Event(new() { DateRule = DateRuleEnum.Easter }).NextDate(new DateOnly(2026, 12, 31)).ShouldBe(new DateOnly(2027, 3, 28));

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public void Should_ReturnNewYear_When_EventIsOnJanuaryFirstAndTodayIsNewYearsEve()
        => Fixed(1, 1).NextDate(new DateOnly(2026, 12, 31)).ShouldBe(new DateOnly(2027, 1, 1));
}
