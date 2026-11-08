using Nextflow.Application.Forecast;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>Horários da análise semanal (RN-DF18, T-DF11), no relógio local de negócio.</summary>
public class ForecastScheduleTests
{
    private static readonly TimeOnly SixAm = new(6, 0);

    [Theory]
    [InlineData("2026-11-08 03:00", "2026-11-09 06:00")] // domingo de madrugada → segunda
    [InlineData("2026-11-09 05:59", "2026-11-09 06:00")] // segunda antes do horário → hoje
    [InlineData("2026-11-09 06:00", "2026-11-16 06:00")] // exatamente no horário → semana seguinte
    [InlineData("2026-11-10 12:00", "2026-11-16 06:00")] // terça → próxima segunda
    [Trait("Rule", "RN-DF18")]
    public void Should_FindNextWeeklySlot_When_GivenNow(string now, string expected)
        => ForecastSchedule.NextSlot(DateTime.Parse(now), DayOfWeek.Monday, SixAm).ShouldBe(DateTime.Parse(expected));

    [Theory]
    [InlineData("2026-11-09 06:00", "2026-11-09 06:00")] // no horário conta como passado
    [InlineData("2026-11-09 05:59", "2026-11-02 06:00")]
    [InlineData("2026-11-12 18:30", "2026-11-09 06:00")]
    [Trait("Rule", "RN-DF18")]
    public void Should_FindLastWeeklySlot_When_GivenNow(string now, string expected)
        => ForecastSchedule.LastSlot(DateTime.Parse(now), DayOfWeek.Monday, SixAm).ShouldBe(DateTime.Parse(expected));

    [Fact]
    [Trait("Rule", "RN-DF18")]
    public void Should_ConvertLocalSlotToUtc_When_BusinessIsThreeHoursBehind()
    {
        var utcNow = new DateTime(2026, 11, 8, 6, 0, 7, DateTimeKind.Utc);
        var localNow = new DateTime(2026, 11, 8, 3, 0, 7);

        var utc = ForecastSchedule.ToUtc(new DateTime(2026, 11, 9, 6, 0, 0), localNow, utcNow);

        utc.ShouldBe(new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc));
        utc.Kind.ShouldBe(DateTimeKind.Utc);
    }
}
