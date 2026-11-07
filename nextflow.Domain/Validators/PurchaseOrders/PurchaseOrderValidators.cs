using FluentValidation;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Models;

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

/// <summary>
/// Formato do recebimento (T-CP4). Itens contra o pedido, status, lote obrigatório por produto e soma dos lotes igual à
/// quantidade do item ficam no use case (dependem do pedido gravado).
/// </summary>
public class ReceivePurchaseOrderDtoValidator : AbstractValidator<ReceivePurchaseOrderDto>
{
    public ReceivePurchaseOrderDtoValidator()
    {
        RuleFor(x => x.Version).NotNull().WithMessage("Informe a versão do pedido.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Informe os itens recebidos.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).NotEmpty().WithMessage("Informe o item.");
            item.RuleFor(i => i.Lots)
                .Must(lots => lots is null || lots.Select(l => Lot.Normalize(l.Code)).Distinct().Count() == lots.Count)
                .WithMessage("O mesmo lote aparece duas vezes no item.");
            item.RuleForEach(i => i.Lots).ChildRules(lot =>
            {
                lot.RuleFor(l => l.Code).Cascade(CascadeMode.Stop)
                    .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Informe o código do lote.")
                    .Must(code => code.Trim().Length <= Lot.CodeMaxLength).WithMessage($"O código do lote deve ter no máximo {Lot.CodeMaxLength} caracteres.");
                lot.RuleFor(l => l.ExpiresOn).NotEmpty().WithMessage("Informe a validade do lote.");
                lot.RuleFor(l => l.Quantity)
                    .GreaterThan(0).WithMessage("A quantidade do lote precisa ser maior que zero.")
                    .PrecisionScale(14, 3, true).WithMessage("A quantidade aceita até 3 casas decimais.");
            });
        });
    }
}
