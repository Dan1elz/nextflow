namespace Nextflow.Domain.Enums;

/// <summary>Estado do pedido (RN-VD16): orçamento com validade ou venda paga; Expirado é do orçamento vencido.</summary>
public enum OrderStatusEnum : byte
{
    Budget = 1,
    Paid = 2,
    Canceled = 3,
    Refunded = 4,
    Expired = 5,
}

public static class OrderStatusEnumExtensions
{
    public static string GetName(this OrderStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this OrderStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<OrderStatusEnum, string> Names = new()
    {
        { OrderStatusEnum.Budget, "Orçamento" },
        { OrderStatusEnum.Paid, "Paga" },
        { OrderStatusEnum.Canceled, "Cancelado" },
        { OrderStatusEnum.Refunded, "Reembolsada" },
        { OrderStatusEnum.Expired, "Expirado" },
    };

    public static readonly Dictionary<OrderStatusEnum, string> ApiNames = new()
    {
        { OrderStatusEnum.Budget, "Budget" },
        { OrderStatusEnum.Paid, "Paid" },
        { OrderStatusEnum.Canceled, "Canceled" },
        { OrderStatusEnum.Refunded, "Refunded" },
        { OrderStatusEnum.Expired, "Expired" },
    };
}
