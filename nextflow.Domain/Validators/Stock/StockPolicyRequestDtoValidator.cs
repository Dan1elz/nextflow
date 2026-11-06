using FluentValidation;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Validators.Stock;

/// <summary>Política da classe ABC (T13, RN-CE29): dias inteiros de 0 a 365.</summary>
public class StockPolicyRequestDtoValidator : AbstractValidator<StockPolicyRequestDto>
{
    public const int MaxDays = 365;

    public StockPolicyRequestDtoValidator()
    {
        RuleFor(x => x.TargetCoverageDays)
            .InclusiveBetween(0, MaxDays).WithMessage($"A cobertura alvo deve ser de 0 a {MaxDays} dias.");
        RuleFor(x => x.SafetyDays)
            .InclusiveBetween(0, MaxDays).WithMessage($"Os dias de segurança devem ser de 0 a {MaxDays}.");
    }
}
