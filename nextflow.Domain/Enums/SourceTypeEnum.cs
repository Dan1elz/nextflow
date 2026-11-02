namespace Nextflow.Domain.Enums;

/// <summary>Documento que originou a movimentação (contrato, enums do módulo). Ajuste e saldo de abertura são <see cref="Manual"/>.</summary>
public enum SourceTypeEnum : byte
{
    Order = 1,
    PurchaseOrder = 2,
    Transfer = 3,
    InventoryCount = 4,
    Manual = 5,
}

public static class SourceTypeEnumExtensions
{
    public static string GetName(this SourceTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this SourceTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<SourceTypeEnum, string> Names = new()
    {
        { SourceTypeEnum.Order, "Pedido" },
        { SourceTypeEnum.PurchaseOrder, "Pedido de compra" },
        { SourceTypeEnum.Transfer, "Transferência" },
        { SourceTypeEnum.InventoryCount, "Inventário" },
        { SourceTypeEnum.Manual, "Manual" },
    };

    public static readonly Dictionary<SourceTypeEnum, string> ApiNames = new()
    {
        { SourceTypeEnum.Order, "Order" },
        { SourceTypeEnum.PurchaseOrder, "PurchaseOrder" },
        { SourceTypeEnum.Transfer, "Transfer" },
        { SourceTypeEnum.InventoryCount, "InventoryCount" },
        { SourceTypeEnum.Manual, "Manual" },
    };
}
