namespace Nextflow.Domain.Enums;

public enum ContactTypeEnum : byte
{
    Phone = 1,
    Email = 2,
    WhatsApp = 3,
}

public static class ContactTypeEnumExtensions
{
    public static string GetName(this ContactTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this ContactTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ContactTypeEnum, string> Names = new()
    {
        { ContactTypeEnum.Phone, "Telefone" },
        { ContactTypeEnum.Email, "E-mail" },
        { ContactTypeEnum.WhatsApp, "WhatsApp" },
    };

    public static readonly Dictionary<ContactTypeEnum, string> ApiNames = new()
    {
        { ContactTypeEnum.Phone, "Phone" },
        { ContactTypeEnum.Email, "Email" },
        { ContactTypeEnum.WhatsApp, "WhatsApp" },
    };
}
