namespace Nextflow.Domain.Enums;

/// <summary>Canal do pedido: venda rápida de balcão (T-VD2) ou pedido com orçamento (T-VD4).</summary>
public enum OrderChannelEnum : byte
{
    Counter = 1,
    Order = 2,
}

public static class OrderChannelEnumExtensions
{
    public static string GetName(this OrderChannelEnum @enum) => Names[@enum];
    public static string GetApiName(this OrderChannelEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<OrderChannelEnum, string> Names = new()
    {
        { OrderChannelEnum.Counter, "Balcão" },
        { OrderChannelEnum.Order, "Pedido" },
    };

    public static readonly Dictionary<OrderChannelEnum, string> ApiNames = new()
    {
        { OrderChannelEnum.Counter, "Counter" },
        { OrderChannelEnum.Order, "Order" },
    };
}
