namespace Nextflow.Domain.Enums;

/// <summary>Motivo da movimentação (contrato, enums do módulo). Perdas não contam como demanda (RN-CE24).</summary>
public enum MovementReasonEnum : byte
{
    Opening = 1,
    Purchase = 2,
    Sale = 3,
    CustomerReturn = 4,
    SupplierReturn = 5,
    TransferIn = 6,
    TransferOut = 7,
    CountAdjustment = 8,
    Adjustment = 9,
    Breakage = 10,
    Expired = 11,
    Theft = 12,
    InternalUse = 13,
}

public static class MovementReasonEnumExtensions
{
    public static string GetName(this MovementReasonEnum @enum) => Names[@enum];
    public static string GetApiName(this MovementReasonEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<MovementReasonEnum, string> Names = new()
    {
        { MovementReasonEnum.Opening, "Saldo de abertura" },
        { MovementReasonEnum.Purchase, "Compra" },
        { MovementReasonEnum.Sale, "Venda" },
        { MovementReasonEnum.CustomerReturn, "Devolução de cliente" },
        { MovementReasonEnum.SupplierReturn, "Devolução ao fornecedor" },
        { MovementReasonEnum.TransferIn, "Transferência (entrada)" },
        { MovementReasonEnum.TransferOut, "Transferência (saída)" },
        { MovementReasonEnum.CountAdjustment, "Ajuste de inventário" },
        { MovementReasonEnum.Adjustment, "Ajuste" },
        { MovementReasonEnum.Breakage, "Avaria" },
        { MovementReasonEnum.Expired, "Vencido" },
        { MovementReasonEnum.Theft, "Furto" },
        { MovementReasonEnum.InternalUse, "Uso interno" },
    };

    public static readonly Dictionary<MovementReasonEnum, string> ApiNames = new()
    {
        { MovementReasonEnum.Opening, "Opening" },
        { MovementReasonEnum.Purchase, "Purchase" },
        { MovementReasonEnum.Sale, "Sale" },
        { MovementReasonEnum.CustomerReturn, "CustomerReturn" },
        { MovementReasonEnum.SupplierReturn, "SupplierReturn" },
        { MovementReasonEnum.TransferIn, "TransferIn" },
        { MovementReasonEnum.TransferOut, "TransferOut" },
        { MovementReasonEnum.CountAdjustment, "CountAdjustment" },
        { MovementReasonEnum.Adjustment, "Adjustment" },
        { MovementReasonEnum.Breakage, "Breakage" },
        { MovementReasonEnum.Expired, "Expired" },
        { MovementReasonEnum.Theft, "Theft" },
        { MovementReasonEnum.InternalUse, "InternalUse" },
    };
}
