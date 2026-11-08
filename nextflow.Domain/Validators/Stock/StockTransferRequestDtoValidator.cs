using FluentValidation;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Validators.Stock;

/// <summary>Formato da transferência (RN-CE62). Local ativo, saldo e negativo (RN-CE32) ficam com o <c>IStockLedger</c>.</summary>
public class StockTransferRequestDtoValidator : AbstractValidator<StockTransferRequestDto>
{
    public const int NoteMaxLength = 500;

    public StockTransferRequestDtoValidator()
    {
        RuleFor(x => x.FromLocationId).NotEmpty().WithMessage("Escolha o local de origem.");
        RuleFor(x => x.ToLocationId)
            .NotEmpty().WithMessage("Escolha o local de destino.")
            .NotEqual(x => x.FromLocationId).WithMessage("O destino precisa ser diferente da origem.");
        RuleFor(x => x.Note).MaximumLength(NoteMaxLength).WithMessage($"A observação tem no máximo {NoteMaxLength} caracteres.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item.")
            .Must(items => items.Select(i => i.VariantId).Distinct().Count() == items.Count)
            .WithMessage("A mesma variação aparece mais de uma vez.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotEmpty().WithMessage("Escolha a variação.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("A quantidade precisa ser maior que zero.");
        });
    }
}
