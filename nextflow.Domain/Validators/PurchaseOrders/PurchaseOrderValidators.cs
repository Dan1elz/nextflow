using FluentValidation;
using Nextflow.Domain.Dtos.PurchaseOrders;

namespace Nextflow.Domain.Validators.PurchaseOrders;

/// <summary>
/// Formato do pedido de compra (T-CP2). Status, fornecedor e local ativos, variação válida, casas decimais do produto e os
/// avisos de mínimo, múltiplo e fora de linha dependem do banco e ficam no use case.
/// </summary>
public class SavePurchaseOrderDtoValidator : AbstractValidator<SavePurchaseOrderDto>
{
    public SavePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty().WithMessage("Escolha o fornecedor.");
        RuleFor(x => x.ReceiveLocationId).NotEmpty().WithMessage("Escolha o local de entrega.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("A observação aceita até 500 caracteres.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item."); // RN-CP01

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotEmpty().WithMessage("Escolha a variação.");
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0).WithMessage("A quantidade precisa ser maior que zero.")
                .PrecisionScale(14, 3, true).WithMessage("A quantidade aceita até 3 casas decimais.");
            item.RuleFor(i => i.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("O custo não pode ser negativo.")
                .PrecisionScale(14, 4, true).WithMessage("O custo aceita até 4 casas decimais.");
        });
    }
}

/// <summary>Cancelamento (T-CP5): motivo obrigatório.</summary>
public class CancelPurchaseOrderDtoValidator : AbstractValidator<CancelPurchaseOrderDto>
{
    public CancelPurchaseOrderDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(500).WithMessage("O motivo aceita até 500 caracteres.");
    }
}
