using FluentValidation;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Validators.Stock;

/// <summary>
/// Formato do lançamento manual (T9). Venda, compra, transferência e inventário só nascem do próprio documento (RN-CE02);
/// só o saldo de abertura informa custo (L5); o ajuste genérico exige observação (L4). Saldo e negativo (RN-CE32) ficam com
/// o <c>IStockLedger</c>.
/// </summary>
public class ManualMovementRequestDtoValidator : AbstractValidator<ManualMovementRequestDto>
{
    /// <summary>Motivos que a tela T9 oferece. Os demais pertencem a um documento (venda, compra, transferência, inventário).</summary>
    public static readonly IReadOnlySet<MovementReasonEnum> ManualReasons = new HashSet<MovementReasonEnum>
    {
        MovementReasonEnum.Opening,
        MovementReasonEnum.Adjustment,
        MovementReasonEnum.Breakage,
        MovementReasonEnum.Expired,
        MovementReasonEnum.Theft,
        MovementReasonEnum.InternalUse,
    };

    /// <summary>Perdas e uso interno só tiram do estoque.</summary>
    public static readonly IReadOnlySet<MovementReasonEnum> OutboundOnlyReasons = new HashSet<MovementReasonEnum>
    {
        MovementReasonEnum.Breakage,
        MovementReasonEnum.Expired,
        MovementReasonEnum.Theft,
        MovementReasonEnum.InternalUse,
    };

    public const string SystemReasonMessage = "Venda, compra, transferência e inventário são lançados pelo próprio documento.";
    public const string UnitCostMessage = "Só o saldo de abertura informa custo.";
    public const string NoteRequiredMessage = "Informe a observação do ajuste.";
    public const string LotAndNewLotMessage = "Escolha um lote existente ou crie um novo, não os dois.";
    public const string NewLotOnlyInboundMessage = "Lote novo só na entrada.";

    public ManualMovementRequestDtoValidator()
    {
        RuleFor(x => x.VariantId).NotEmpty().WithMessage("Escolha a variação.");
        RuleFor(x => x.LocationId).NotEmpty().WithMessage("Escolha o local.");

        // Lote (F1.7): existente ou novo (código + validade), nunca os dois; lote novo só entra. Se o produto controla lote
        // quem decide é o livro-razão.
        RuleFor(x => x.NewLot).Null().WithMessage(LotAndNewLotMessage).When(x => x.LotId.HasValue);
        RuleFor(x => x.NewLot).Null().WithMessage(NewLotOnlyInboundMessage).When(x => x.Type == MovementTypeEnum.Out);
        RuleFor(x => x.NewLot!.Code).Cascade(CascadeMode.Stop)
            .Must(code => !string.IsNullOrWhiteSpace(code)).WithMessage("Informe o código do lote.")
            .Must(code => code.Trim().Length <= Lot.CodeMaxLength).WithMessage($"O código do lote deve ter no máximo {Lot.CodeMaxLength} caracteres.")
            .OverridePropertyName("newLot.code")
            .When(x => x.NewLot is not null);
        RuleFor(x => x.NewLot!.ExpiresOn).NotEmpty().WithMessage("Informe a validade do lote.")
            .OverridePropertyName("newLot.expiresOn")
            .When(x => x.NewLot is not null);

        RuleFor(x => x.Type).IsInEnum().WithMessage("Escolha entrada ou saída.");
        RuleFor(x => x.Reason).Cascade(CascadeMode.Stop)
            .IsInEnum().WithMessage("Escolha o motivo.")
            .Must(ManualReasons.Contains).WithMessage(SystemReasonMessage); // RN-CE02

        RuleFor(x => x.Type)
            .Equal(MovementTypeEnum.In).WithMessage("O saldo de abertura é sempre uma entrada.")
            .When(x => x.Reason == MovementReasonEnum.Opening);
        RuleFor(x => x.Type)
            .Equal(MovementTypeEnum.Out).WithMessage("Perda e uso interno são sempre uma saída.")
            .When(x => OutboundOnlyReasons.Contains(x.Reason));

        RuleFor(x => x.Quantity)
            .GreaterThan(0).WithMessage("A quantidade precisa ser maior que zero.")
            .PrecisionScale(14, 3, true).WithMessage("A quantidade aceita até 3 casas decimais.");

        RuleFor(x => x.UnitCost) // L5
            .Null().WithMessage(UnitCostMessage)
            .When(x => x.Reason != MovementReasonEnum.Opening);
        RuleFor(x => x.UnitCost!.Value)
            .GreaterThan(0).WithMessage("O custo precisa ser maior que zero.")
            .PrecisionScale(14, 4, true).WithMessage("O custo aceita até 4 casas decimais.")
            .OverridePropertyName(nameof(ManualMovementRequestDto.UnitCost))
            .When(x => x.Reason == MovementReasonEnum.Opening && x.UnitCost.HasValue);

        RuleFor(x => x.Note) // L4
            .NotEmpty().WithMessage(NoteRequiredMessage)
            .When(x => x.Reason == MovementReasonEnum.Adjustment);
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("A observação deve ter no máximo 500 caracteres.");
    }
}
