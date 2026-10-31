namespace Nextflow.Domain.Enums;

/// <summary>Dono de endereço e contato (RN-CD05). Sem FK no banco: CHECK de valor e existência validada no use case.</summary>
public enum OwnerTypeEnum : byte
{
    Client = 1,
    Supplier = 2,
}

public static class OwnerTypeEnumExtensions
{
    public static string GetName(this OwnerTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this OwnerTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<OwnerTypeEnum, string> Names = new()
    {
        { OwnerTypeEnum.Client, "Cliente" },
        { OwnerTypeEnum.Supplier, "Fornecedor" },
    };

    public static readonly Dictionary<OwnerTypeEnum, string> ApiNames = new()
    {
        { OwnerTypeEnum.Client, "Client" },
        { OwnerTypeEnum.Supplier, "Supplier" },
    };
}
