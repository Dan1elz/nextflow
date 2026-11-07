namespace Nextflow.Domain.Enums;

/// <summary>Status do pedido de compra: Orçamento → Pendente (enviado) → Recebido, ou Cancelado (RN-CP07, RN-CP08).</summary>
public enum PurchaseStatusEnum : byte
{
    Budget = 1,
    Pending = 2,
    Received = 3,
    Canceled = 4,
}

public static class PurchaseStatusEnumExtensions
{
    public static string GetName(this PurchaseStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this PurchaseStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<PurchaseStatusEnum, string> Names = new()
    {
        { PurchaseStatusEnum.Budget, "Orçamento" },
        { PurchaseStatusEnum.Pending, "Pendente" },
        { PurchaseStatusEnum.Received, "Recebido" },
        { PurchaseStatusEnum.Canceled, "Cancelado" },
    };

    public static readonly Dictionary<PurchaseStatusEnum, string> ApiNames = new()
    {
        { PurchaseStatusEnum.Budget, "Budget" },
        { PurchaseStatusEnum.Pending, "Pending" },
        { PurchaseStatusEnum.Received, "Received" },
        { PurchaseStatusEnum.Canceled, "Canceled" },
    };
}
