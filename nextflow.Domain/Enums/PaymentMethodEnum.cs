namespace Nextflow.Domain.Enums;

/// <summary>Forma de pagamento, sempre à vista (RN-VD18); sem boleto (RN-VD20).</summary>
public enum PaymentMethodEnum : byte
{
    Cash = 1,
    CreditCard = 2,
    DebitCard = 3,
    BankTransfer = 4,
    Pix = 5,
}

public static class PaymentMethodEnumExtensions
{
    public static string GetName(this PaymentMethodEnum @enum) => Names[@enum];
    public static string GetApiName(this PaymentMethodEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<PaymentMethodEnum, string> Names = new()
    {
        { PaymentMethodEnum.Cash, "Dinheiro" },
        { PaymentMethodEnum.CreditCard, "Cartão de crédito" },
        { PaymentMethodEnum.DebitCard, "Cartão de débito" },
        { PaymentMethodEnum.BankTransfer, "Transferência bancária" },
        { PaymentMethodEnum.Pix, "Pix" },
    };

    public static readonly Dictionary<PaymentMethodEnum, string> ApiNames = new()
    {
        { PaymentMethodEnum.Cash, "Cash" },
        { PaymentMethodEnum.CreditCard, "CreditCard" },
        { PaymentMethodEnum.DebitCard, "DebitCard" },
        { PaymentMethodEnum.BankTransfer, "BankTransfer" },
        { PaymentMethodEnum.Pix, "Pix" },
    };
}
