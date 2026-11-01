using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Catalog;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Catalog;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Seeders;
using NSubstitute;

namespace Nextflow.Tests.Unit.Inventory;

public class CalendarEventRulesTests
{
    /// <summary>Passado de propósito: com o relógio real, o evento de 20/02 cairia em outro ano (RN-CE46).</summary>
    private static readonly DateOnly Today = new(2026, 2, 15);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CalendarEventRequestDtoValidator _validator = new();

    private static IBusinessClock Clock()
    {
        var clock = Substitute.For<IBusinessClock>();
        clock.Today.Returns(Today);
        return clock;
    }

    private static ICalendarEventRepository Events(params CalendarEvent[] events)
    {
        var repository = Substitute.For<ICalendarEventRepository>();
        repository.Exists(Arg.Any<Expression<Func<CalendarEvent, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => events.AsQueryable().Any(call.Arg<Expression<Func<CalendarEvent, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<CalendarEvent>, IQueryable<CalendarEvent>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => events.FirstOrDefault(e => e.Id == call.Arg<Guid>()));
        return repository;
    }

    private CreateCalendarEventUseCase Create(params CalendarEvent[] events)
        => new(Events(events), _unitOfWork, _validator, new ResultFactory<CalendarEventResponseDto>(), Clock());

    private UpdateCalendarEventUseCase Update(params CalendarEvent[] events)
        => new(Events(events), _unitOfWork, _validator, new ResultFactory<CalendarEventResponseDto>(), Clock());

    private DeleteCalendarEventUseCase Delete(params CalendarEvent[] events)
        => new(Events(events), _unitOfWork, new ResultFactory<bool>());

    private ReactivateCalendarEventUseCase Reactivate(params CalendarEvent[] events)
        => new(Events(events), _unitOfWork, new ResultFactory<bool>());

    private static CalendarEventRequestDto FixedDto(string name = "Festa da Uva", int month = 2, int day = 20, int? leadDays = 30)
        => new() { Name = name, DateRule = DateRuleEnum.Fixed, Month = month, Day = day, LeadDays = leadDays };

    private static CalendarEventRequestDto NthWeekdayDto(int month, int weekday, int nth, int leadDays) => new()
    {
        Name = "Dia das Mães",
        DateRule = DateRuleEnum.NthWeekday,
        Month = month,
        NthWeekday = new NthWeekdayRequestDto { Weekday = weekday, Nth = nth },
        LeadDays = leadDays,
    };

    private static CalendarEventRequestDto EasterOffsetDto(int days, int leadDays)
        => new() { Name = "Carnaval", DateRule = DateRuleEnum.EasterOffset, EasterOffsetDays = days, LeadDays = leadDays };

    private static CalendarEvent Company(string name = "Festa da Uva") => new(FixedDto(name), EventScopeEnum.Company);

    private static CalendarEvent National(string name = "Natal", int month = 12, int day = 25)
        => new(FixedDto(name, month, day, 54), EventScopeEnum.National);

    private void ShouldFailOn(CalendarEventRequestDto dto, string field)
    {
        var result = _validator.Validate(dto);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.PropertyName).ShouldContain(field);
    }

    private void ShouldPass(CalendarEventRequestDto dto)
        => _validator.Validate(dto).IsValid.ShouldBeTrue();

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_Pass_When_FixedDateExists() => ShouldPass(FixedDto(month: 2, day: 28));

    [Theory]
    [InlineData(null, 10, "Month")]
    [InlineData(13, 10, "Month")]
    [InlineData(0, 10, "Month")]
    [InlineData(6, null, "Day")]
    [InlineData(4, 31, "Day")]
    [InlineData(6, 0, "Day")]
    [InlineData(2, 30, "Day")]
    [InlineData(2, 29, "Day")]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnField_When_FixedDateIsInvalid(int? month, int? day, string field)
        => ShouldFailOn(new CalendarEventRequestDto { Name = "A", DateRule = DateRuleEnum.Fixed, Month = month, Day = day, LeadDays = 1 }, field);

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_ExplainNoLeapDay_When_DayIsFebruary29()
    {
        var result = _validator.Validate(FixedDto(month: 2, day: 29));

        result.Errors.Single().ErrorMessage.ShouldContain("29 de fevereiro");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 4)]
    [Trait("Rule", "RN-CE41")]
    public void Should_Pass_When_NthWeekdayIsComplete(int weekday, int nth)
        => ShouldPass(new CalendarEventRequestDto
        {
            Name = "A",
            DateRule = DateRuleEnum.NthWeekday,
            Month = 5,
            NthWeekday = new NthWeekdayRequestDto { Weekday = weekday, Nth = nth },
            LeadDays = 1,
        });

    [Theory]
    [InlineData(null, 2, "NthWeekday.Weekday")]
    [InlineData(7, 2, "NthWeekday.Weekday")]
    [InlineData(-1, 2, "NthWeekday.Weekday")]
    [InlineData(0, null, "NthWeekday.Nth")]
    [InlineData(0, 0, "NthWeekday.Nth")]
    [InlineData(0, 5, "NthWeekday.Nth")]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnField_When_NthWeekdayIsInvalid(int? weekday, int? nth, string field)
        => ShouldFailOn(new CalendarEventRequestDto
        {
            Name = "A",
            DateRule = DateRuleEnum.NthWeekday,
            Month = 5,
            NthWeekday = new NthWeekdayRequestDto { Weekday = weekday, Nth = nth },
            LeadDays = 1,
        }, field);

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnNthWeekday_When_ObjectIsMissing()
        => ShouldFailOn(new CalendarEventRequestDto { Name = "A", DateRule = DateRuleEnum.NthWeekday, Month = 5, LeadDays = 1 }, "NthWeekday");

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnMonth_When_NthWeekdayHasNoMonth()
        => ShouldFailOn(new CalendarEventRequestDto
        {
            Name = "A",
            DateRule = DateRuleEnum.NthWeekday,
            NthWeekday = new NthWeekdayRequestDto { Weekday = 0, Nth = 1 },
            LeadDays = 1,
        }, "Month");

    [Theory]
    [InlineData(-60)]
    [InlineData(-47)]
    [InlineData(0)]
    [InlineData(60)]
    [Trait("Rule", "RN-CE41")]
    public void Should_Pass_When_EasterOffsetIsInRange(int offset)
        => ShouldPass(new CalendarEventRequestDto { Name = "A", DateRule = DateRuleEnum.EasterOffset, EasterOffsetDays = offset, LeadDays = 1 });

    [Theory]
    [InlineData(null)]
    [InlineData(-61)]
    [InlineData(61)]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnOffset_When_EasterOffsetIsInvalid(int? offset)
        => ShouldFailOn(new CalendarEventRequestDto { Name = "A", DateRule = DateRuleEnum.EasterOffset, EasterOffsetDays = offset, LeadDays = 1 }, "EasterOffsetDays");

    [Theory]
    [InlineData(DateRuleEnum.Easter)]
    [InlineData(DateRuleEnum.BlackFriday)]
    [Trait("Rule", "RN-CE41")]
    public void Should_RequireNoExtraField_When_RuleIsEasterOrBlackFriday(DateRuleEnum rule)
        => ShouldPass(new CalendarEventRequestDto { Name = "A", DateRule = rule, LeadDays = 1 });

    [Fact]
    [Trait("Rule", "RN-CE41")]
    public void Should_FailOnDateRule_When_RuleIsMissing()
        => ShouldFailOn(new CalendarEventRequestDto { Name = "A", LeadDays = 1 }, "DateRule");

    [Theory]
    [InlineData(DateRuleEnum.Easter)]
    [InlineData(DateRuleEnum.BlackFriday)]
    [InlineData(DateRuleEnum.EasterOffset)]
    [InlineData(DateRuleEnum.NthWeekday)]
    [InlineData(DateRuleEnum.Fixed)]
    [Trait("Rule", "RN-CE41")]
    public void Should_StoreNull_When_FieldDoesNotBelongToRule(DateRuleEnum rule)
    {
        var entity = new CalendarEvent(new CalendarEventRequestDto
        {
            Name = "A",
            DateRule = rule,
            Month = 3,
            Day = 4,
            NthWeekday = new NthWeekdayRequestDto { Weekday = 1, Nth = 2 },
            EasterOffsetDays = -47,
            LeadDays = 1,
        }, EventScopeEnum.Company);

        (entity.Month is null).ShouldBe(rule is not (DateRuleEnum.Fixed or DateRuleEnum.NthWeekday));
        (entity.Day is null).ShouldBe(rule != DateRuleEnum.Fixed);
        (entity.Weekday is null).ShouldBe(rule != DateRuleEnum.NthWeekday);
        (entity.Nth is null).ShouldBe(rule != DateRuleEnum.NthWeekday);
        (entity.EasterOffsetDays is null).ShouldBe(rule != DateRuleEnum.EasterOffset);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(121)]
    [Trait("Rule", "RN-CE45")]
    public void Should_FailOnLeadDays_When_OutOfRangeOrMissing(int? leadDays)
        => ShouldFailOn(FixedDto(leadDays: leadDays), "LeadDays");

    [Theory]
    [InlineData(0)]
    [InlineData(120)]
    [Trait("Rule", "RN-CE45")]
    public void Should_Pass_When_LeadDaysIsOnTheLimit(int leadDays) => ShouldPass(FixedDto(leadDays: leadDays));

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_CreateAsCompany_When_Posting()
    {
        var result = await Create().Execute(FixedDto());

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Scope.ShouldBe("Company");
        result.Data.ProductCount.ShouldBe(0);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE46")]
    public async Task Should_CalculateNextDateWithBusinessClock_When_Creating()
    {
        var result = await Create().Execute(FixedDto(month: 2, day: 20));

        result.Data!.NextDate.ShouldBe(new DateOnly(2026, 2, 20));
    }

    [Theory]
    [InlineData("Natal")]
    [InlineData("NATAL")]
    [InlineData("  natal ")]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_NameIsTakenByANationalEvent(string name)
    {
        var result = await Create(National()).Execute(FixedDto(name));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("name");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_Return422OnName_When_RenamingToATakenName()
    {
        var other = Company("Outra");
        var entity = Company();

        var result = await Update(other, entity).Execute(entity.Id, FixedDto("outra"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("name");
        entity.Name.ShouldBe("Festa da Uva");
    }

    [Fact]
    [Trait("Rule", "RN-CE44")]
    public async Task Should_AcceptOwnName_When_UpdatingEvent()
    {
        var entity = Company();

        (await Update(entity).Execute(entity.Id, FixedDto(leadDays: 45))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_ChangeOnlyLeadDays_When_NationalKeepsNameAndRule()
    {
        var entity = National();

        var result = await Update(entity).Execute(entity.Id, FixedDto("Natal", 12, 25, 60));

        result.IsSuccess.ShouldBeTrue();
        entity.LeadDays.ShouldBe((short)60);
        entity.Scope.ShouldBe(EventScopeEnum.National);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalChangesName()
    {
        var entity = National();

        var result = await Update(entity).Execute(entity.Id, FixedDto("Natalzão", 12, 25, 60));

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldContain("antecedência");
        entity.Name.ShouldBe("Natal");
        entity.LeadDays.ShouldBe((short)54);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalChangesFixedDate()
    {
        var entity = National();

        (await Update(entity).Execute(entity.Id, FixedDto("Natal", 12, 24, 60))).ErrorCode.ShouldBe(400);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalChangesDateRule()
    {
        var entity = National();
        var dto = new CalendarEventRequestDto { Name = "Natal", DateRule = DateRuleEnum.Easter, LeadDays = 60 };

        (await Update(entity).Execute(entity.Id, dto)).ErrorCode.ShouldBe(400);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalFixedChangesMonth()
    {
        var entity = National();

        (await Update(entity).Execute(entity.Id, FixedDto("Natal", 11, 25, 60))).ErrorCode.ShouldBe(400);
        entity.Month.ShouldBe((short)12);
        entity.LeadDays.ShouldBe((short)54);
    }

    [Theory]
    [InlineData(6, 0, 2)] // outro mês
    [InlineData(5, 1, 2)] // outro dia da semana
    [InlineData(5, 0, 3)] // outra ocorrência
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalNthWeekdayChangesField(int month, int weekday, int nth)
    {
        var entity = new CalendarEvent(NthWeekdayDto(5, 0, 2, 21), EventScopeEnum.National);

        (await Update(entity).Execute(entity.Id, NthWeekdayDto(month, weekday, nth, 30))).ErrorCode.ShouldBe(400);
        entity.LeadDays.ShouldBe((short)21);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_Return400_When_NationalEasterOffsetChangesDays()
    {
        var entity = new CalendarEvent(EasterOffsetDto(-47, 10), EventScopeEnum.National);

        (await Update(entity).Execute(entity.Id, EasterOffsetDto(-46, 30))).ErrorCode.ShouldBe(400);
        entity.EasterOffsetDays.ShouldBe((short)-47);
        entity.LeadDays.ShouldBe((short)10);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_ChangeLeadDays_When_NationalKeepsNthWeekdayOrEasterOffset()
    {
        var nthWeekday = new CalendarEvent(NthWeekdayDto(5, 0, 2, 21), EventScopeEnum.National);
        var easterOffset = new CalendarEvent(EasterOffsetDto(-47, 10), EventScopeEnum.National);

        (await Update(nthWeekday).Execute(nthWeekday.Id, NthWeekdayDto(5, 0, 2, 30))).IsSuccess.ShouldBeTrue();
        (await Update(easterOffset).Execute(easterOffset.Id, EasterOffsetDto(-47, 30))).IsSuccess.ShouldBeTrue();

        nthWeekday.LeadDays.ShouldBe((short)30);
        easterOffset.LeadDays.ShouldBe((short)30);
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_IgnoreFieldsOutsideTheRule_When_ComparingNationalDefinition()
    {
        var entity = new CalendarEvent(new CalendarEventRequestDto { Name = "Páscoa", DateRule = DateRuleEnum.Easter, LeadDays = 42 }, EventScopeEnum.National);
        var dto = new CalendarEventRequestDto { Name = "Páscoa", DateRule = DateRuleEnum.Easter, Month = 3, Day = 1, LeadDays = 50 };

        (await Update(entity).Execute(entity.Id, dto)).IsSuccess.ShouldBeTrue();
        entity.Month.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE42")]
    public async Task Should_EditEverything_When_EventBelongsToCompany()
    {
        var entity = Company();
        var dto = new CalendarEventRequestDto { Name = "Carnaval", DateRule = DateRuleEnum.EasterOffset, EasterOffsetDays = -47, LeadDays = 5 };

        var result = await Update(entity).Execute(entity.Id, dto);

        result.IsSuccess.ShouldBeTrue();
        entity.Name.ShouldBe("Carnaval");
        entity.DateRule.ShouldBe(DateRuleEnum.EasterOffset);
        entity.Month.ShouldBeNull();
        entity.Day.ShouldBeNull();
        entity.EasterOffsetDays.ShouldBe((short)-47);
        entity.Scope.ShouldBe(EventScopeEnum.Company);
    }

    [Theory]
    [InlineData(EventScopeEnum.National)]
    [InlineData(EventScopeEnum.Company)]
    [Trait("Rule", "RN-CE43")]
    public async Task Should_SoftDeleteAndReactivate_When_EitherScope(EventScopeEnum scope)
    {
        var entity = new CalendarEvent(FixedDto(), scope);

        (await Delete(entity).Execute(entity.Id, Guid.NewGuid())).IsSuccess.ShouldBeTrue();
        entity.IsActive.ShouldBeFalse();

        (await Reactivate(entity).Execute(entity.Id, Guid.NewGuid())).IsSuccess.ShouldBeTrue();
        entity.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_AddSevenNationalEventsAndCommitOnce_When_DatabaseIsEmpty()
    {
        var added = new List<CalendarEvent>();
        var repository = Events();
        await repository.AddAsync(Arg.Do<CalendarEvent>(added.Add), Arg.Any<CancellationToken>());

        await new CalendarEventsSeeder(repository, _unitOfWork).SeedAsync(default);

        added.Count.ShouldBe(7);
        added.ShouldAllBe(e => e.Scope == EventScopeEnum.National && e.IsActive);
        added.ShouldAllBe(e => _validator.Validate(new CalendarEventRequestDto
        {
            Name = e.Name,
            DateRule = e.DateRule,
            Month = e.Month,
            Day = e.Day,
            NthWeekday = e.Weekday == null ? null : new NthWeekdayRequestDto { Weekday = e.Weekday, Nth = e.Nth },
            LeadDays = e.LeadDays,
        }).IsValid);
        added.Select(e => e.DateIn(2026)).ShouldBe(CommercialCalendar.GetEvents(2026).Select(e => e.Date), ignoreOrder: true);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CommercialCalendar.EasterName, 42)]
    [InlineData(CommercialCalendar.MothersDayName, 21)]
    [InlineData(CommercialCalendar.ValentinesDayName, 21)]
    [InlineData(CommercialCalendar.FathersDayName, 10)]
    [InlineData(CommercialCalendar.ChildrensDayName, 14)]
    [InlineData(CommercialCalendar.BlackFridayName, 15)]
    [InlineData(CommercialCalendar.ChristmasName, 54)]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_UseDocumentedLeadDays_When_Seeding(string name, int leadDays)
    {
        var added = new List<CalendarEvent>();
        var repository = Events();
        await repository.AddAsync(Arg.Do<CalendarEvent>(added.Add), Arg.Any<CancellationToken>());

        await new CalendarEventsSeeder(repository, _unitOfWork).SeedAsync(default);

        added.Single(e => e.Name == name).LeadDays.ShouldBe((short)leadDays);
    }

    [Fact]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_AddNothingAndNotCommit_When_RunningOverSeededEvents()
    {
        var seeded = new List<CalendarEvent>();
        var first = Events();
        await first.AddAsync(Arg.Do<CalendarEvent>(seeded.Add), Arg.Any<CancellationToken>());
        await new CalendarEventsSeeder(first, _unitOfWork).SeedAsync(default);
        _unitOfWork.ClearReceivedCalls();

        var second = Events([.. seeded]);
        await new CalendarEventsSeeder(second, _unitOfWork).SeedAsync(default);

        await second.DidNotReceive().AddAsync(Arg.Any<CalendarEvent>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE47")]
    public async Task Should_KeepLeadDaysAndStatus_When_EventAlreadyExists()
    {
        var existing = National();
        existing.Update(FixedDto("Natal", 12, 25, 99));
        existing.Delete();
        var added = new List<CalendarEvent>();
        var repository = Events(existing);
        await repository.AddAsync(Arg.Do<CalendarEvent>(added.Add), Arg.Any<CancellationToken>());

        await new CalendarEventsSeeder(repository, _unitOfWork).SeedAsync(default);

        added.Count.ShouldBe(6);
        added.ShouldNotContain(e => e.Name == "Natal");
        existing.LeadDays.ShouldBe((short)99);
        existing.IsActive.ShouldBeFalse();
    }
}
