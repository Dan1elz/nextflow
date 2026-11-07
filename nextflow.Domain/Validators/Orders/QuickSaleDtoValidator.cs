using FluentValidation;
using Nextflow.Domain.Dtos.Orders;

namespace Nextflow.Domain.Validators.Orders;

/// <summary>
/// Formato da venda rápida (T-VD2): ao menos um item (RN-VD01), quantidade positiva, desconto em R$ não negativo
/// (RN-VD03), ao menos um pagamento positivo numa forma válida (RN-VD20). Lote só depois da F1.7. Preço, desconto acima da
/// linha, soma dos pagamentos e saldo dependem do banco e ficam no use case.
/// </summary>
public class QuickSaleDtoValidator : AbstractValidator<QuickSaleDto>
{
    public QuickSaleDtoValidator()
    {
        RuleFor(x => x.StoreId).NotEmpty().WithMessage("Selecione a loja.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Inclua ao menos um item.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.VariantId).NotEmpty().WithMessage("Escolha o produto.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("A quantidade precisa ser maior que zero.");
            item.RuleFor(i => i.Discount)
                .GreaterThanOrEqualTo(0).WithMessage("O desconto não pode ser negativo.")
                .PrecisionScale(12, 2, true).WithMessage("O desconto aceita no máximo duas casas decimais.");
            item.RuleFor(i => i.LotId).Null().WithMessage("A escolha de lote entra com o controle de lotes (F1.7).");
        });
        RuleFor(x => x.Payments).NotEmpty().WithMessage("Informe ao menos um pagamento.");
        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.Method).IsInEnum().WithMessage("Escolha uma forma de pagamento válida.");
            payment.RuleFor(p => p.Amount)
                .GreaterThan(0).WithMessage("O valor precisa ser maior que zero.")
                .PrecisionScale(12, 2, true).WithMessage("O valor aceita no máximo duas casas decimais.");
        });
        RuleFor(x => x.CashReceived)
            .GreaterThanOrEqualTo(0).WithMessage("O valor recebido não pode ser negativo.")
            .PrecisionScale(12, 2, true).WithMessage("O valor recebido aceita no máximo duas casas decimais.")
            .When(x => x.CashReceived.HasValue);
    }
}
