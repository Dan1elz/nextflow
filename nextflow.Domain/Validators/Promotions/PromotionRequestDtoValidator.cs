using FluentValidation;
using Nextflow.Domain.Dtos.Promotions;

namespace Nextflow.Domain.Validators.Promotions;

/// <summary>
/// Formato da promoção (RN-VD10): desconto de 1% a 90%, fim no início ou depois, no máximo 90 dias contando início e fim.
/// O início a partir de hoje depende da data de negócio e fica no use case.
/// </summary>
public class PromotionRequestDtoValidator : AbstractValidator<PromotionRequestDto>
{
    public const decimal MinPercent = 1m;
    public const decimal MaxPercent = 90m;
    public const int MaxDays = 90;
    public const int ReasonMaxLength = 200;

    public PromotionRequestDtoValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("Escolha o produto.");
        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(MinPercent, MaxPercent).WithMessage($"O desconto deve ser de {MinPercent:0}% a {MaxPercent:0}%.")
            .PrecisionScale(5, 2, true).WithMessage("O desconto aceita no máximo duas casas decimais.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Informe o início.");
        RuleFor(x => x.EndDate)
            .NotEmpty().WithMessage("Informe o fim.")
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("O fim deve ser no início ou depois dele.")
            .Must((dto, end) => end.DayNumber - dto.StartDate.DayNumber + 1 <= MaxDays)
            .WithMessage($"A promoção dura no máximo {MaxDays} dias, contando início e fim.");
        RuleFor(x => x.Reason).MaximumLength(ReasonMaxLength).WithMessage($"O motivo tem no máximo {ReasonMaxLength} caracteres.");
    }
}
