using FluentValidation;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Validators.Forecasts;

/// <summary>Formato do "Comprar" (T-DF4): ao menos um grupo, cada um com linhas de quantidade positiva e custo não negativo.</summary>
public class ApplyPurchaseDtoValidator : AbstractValidator<ApplyPurchaseDto>
{
    public ApplyPurchaseDtoValidator()
    {
        RuleFor(x => x.Groups).NotEmpty().WithMessage("Inclua ao menos um fornecedor.");
        RuleForEach(x => x.Groups).ChildRules(group =>
        {
            group.RuleFor(g => g.SupplierId).NotEmpty().WithMessage("Escolha o fornecedor.");
            group.RuleFor(g => g.ReceiveLocationId).NotEmpty().WithMessage("Escolha o local de entrega.");
            group.RuleFor(g => g.Note).MaximumLength(500).WithMessage("A observação aceita até 500 caracteres.");
            group.RuleFor(g => g.Items).NotEmpty().WithMessage("Inclua ao menos um item.");
            group.RuleForEach(g => g.Items).ChildRules(item =>
            {
                item.RuleFor(i => i.RecommendationId).NotEmpty().WithMessage("Informe a recomendação.");
                item.RuleFor(i => i.VariantId).NotEmpty().WithMessage("Informe a variação.");
                item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("A quantidade deve ser maior que zero.");
                item.RuleFor(i => i.UnitCost).GreaterThanOrEqualTo(0).WithMessage("O custo não pode ser negativo.");
            });
        });
    }
}

/// <summary>Formato do "Promover" (T-DF6): desconto de 1% a 90% (RN-VD10) e término a partir do início.</summary>
public class ApplyPromotionDtoValidator : AbstractValidator<ApplyPromotionDto>
{
    public ApplyPromotionDtoValidator()
    {
        RuleFor(x => x.DiscountPercent).InclusiveBetween(1, 90).WithMessage("O desconto deve ser de 1% a 90%.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Informe o início.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("O término deve ser igual ou depois do início.");
    }
}

/// <summary>Formato do descarte (T-DF8): motivo conhecido, se vier; "Other" exige o texto.</summary>
public class DismissRecommendationDtoValidator : AbstractValidator<DismissRecommendationDto>
{
    public DismissRecommendationDtoValidator()
    {
        RuleFor(x => x.ReasonCode)
            .Must(code => code == null || Enum.TryParse<DismissReasonEnum>(code, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(code, out _))
            .WithMessage("Motivo inválido.");
        RuleFor(x => x.Note)
            .NotEmpty().When(x => x.ReasonCode == nameof(DismissReasonEnum.Other)).WithMessage("Descreva o motivo.")
            .MaximumLength(255).WithMessage("O motivo aceita até 255 caracteres.");
    }
}
