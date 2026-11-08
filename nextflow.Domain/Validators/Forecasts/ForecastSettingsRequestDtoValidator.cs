using FluentValidation;
using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Validators.Forecasts;

/// <summary>Formato do agendamento (T-DF11): dia da semana 0 a 6, hora "HH:mm" e lote da IA de 1 a 100 (RN-DF20).</summary>
public class ForecastSettingsRequestDtoValidator : AbstractValidator<ForecastSettingsRequestDto>
{
    public const int MinAiBatchSize = 1;
    public const int MaxAiBatchSize = 100;

    public ForecastSettingsRequestDtoValidator()
    {
        RuleFor(x => x.Weekday)
            .InclusiveBetween(0, 6).WithMessage("O dia da semana deve ser de 0 (domingo) a 6 (sábado).");

        RuleFor(x => x.Time)
            .Must(time => TimeOnly.TryParseExact(time, "HH:mm", out _)).WithMessage("Informe a hora no formato HH:mm.");

        RuleFor(x => x.AiBatchSize)
            .InclusiveBetween(MinAiBatchSize, MaxAiBatchSize).WithMessage($"O lote da IA deve ter de {MinAiBatchSize} a {MaxAiBatchSize} produtos.");
    }
}
