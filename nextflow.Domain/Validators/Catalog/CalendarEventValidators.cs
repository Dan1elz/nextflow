using FluentValidation;
using Nextflow.Domain.Dtos.CalendarEvents;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Validators.Catalog;

public class NthWeekdayRequestDtoValidator : AbstractValidator<NthWeekdayRequestDto>
{
    public NthWeekdayRequestDtoValidator()
    {
        RuleFor(x => x.Weekday)
            .NotNull().WithMessage("Informe o dia da semana.")
            .InclusiveBetween(0, 6).WithMessage("O dia da semana deve ser de 0 (domingo) a 6 (sábado).");
        RuleFor(x => x.Nth) // RN-CE41: o 5º nem sempre existe.
            .NotNull().WithMessage("Informe a ocorrência no mês.")
            .InclusiveBetween(1, 4).WithMessage("A ocorrência no mês deve ser de 1 a 4.");
    }
}

public class CalendarEventRequestDtoValidator : AbstractValidator<CalendarEventRequestDto>
{
    /// <summary>Ano sem 29/02: a data precisa existir em todos os anos (RN-CE41).</summary>
    private const int NonLeapYear = 2025;

    private const int MaxLeadDays = 120;
    private const int MaxEasterOffsetDays = 60;

    public CalendarEventRequestDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Informe o nome.")
            .MaximumLength(100).WithMessage("O nome deve ter no máximo 100 caracteres.");
        RuleFor(x => x.DateRule).IsInEnum().WithMessage("Informe a regra de data: Fixed, Easter, NthWeekday, BlackFriday ou EasterOffset.");
        RuleFor(x => x.LeadDays) // RN-CE45
            .NotNull().WithMessage("Informe a antecedência.")
            .InclusiveBetween(0, MaxLeadDays).WithMessage($"A antecedência deve ser de 0 a {MaxLeadDays} dias.");

        When(x => x.DateRule is DateRuleEnum.Fixed or DateRuleEnum.NthWeekday, () =>
            RuleFor(x => x.Month)
                .NotNull().WithMessage("Informe o mês.")
                .InclusiveBetween(1, 12).WithMessage("O mês deve ser de 1 a 12."));

        When(x => x.DateRule == DateRuleEnum.Fixed, () =>
            RuleFor(x => x.Day)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Informe o dia.")
                .Must((dto, day) => !IsFebruary29(dto.Month, day))
                .WithMessage("29 de fevereiro não é aceito, porque não existe em todos os anos.")
                .Must((dto, day) => DayExists(dto.Month, day.GetValueOrDefault()))
                .WithMessage("Este dia não existe no mês informado."));

        When(x => x.DateRule == DateRuleEnum.NthWeekday, () =>
            RuleFor(x => x.NthWeekday!)
                .NotNull().WithMessage("Informe o dia da semana e a ocorrência no mês.")
                .SetValidator(new NthWeekdayRequestDtoValidator()));

        When(x => x.DateRule == DateRuleEnum.EasterOffset, () =>
            RuleFor(x => x.EasterOffsetDays)
                .NotNull().WithMessage("Informe os dias a partir da Páscoa.")
                .InclusiveBetween(-MaxEasterOffsetDays, MaxEasterOffsetDays)
                .WithMessage($"Os dias a partir da Páscoa devem ser de -{MaxEasterOffsetDays} a {MaxEasterOffsetDays}."));
    }

    private static bool IsFebruary29(int? month, int? day) => month == 2 && day == 29;

    /// <summary>Com mês inválido, o erro já está em <c>month</c>: aqui só se confere o limite de qualquer mês.</summary>
    private static bool DayExists(int? month, int day)
        => day >= 1 && day <= (month is >= 1 and <= 12 ? DateTime.DaysInMonth(NonLeapYear, month.Value) : 31);
}
