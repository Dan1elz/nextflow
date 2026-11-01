namespace Nextflow.Domain.Enums;

/// <summary>Perfil do produto: define os padrões e os campos que a tela mostra, sem tabelas diferentes (RN-CE20).</summary>
public enum ProductTypeEnum : byte
{
    Standard = 1,
    Apparel = 2,
    Perishable = 3,
    Bulk = 4,
}

public static class ProductTypeEnumExtensions
{
    public static string GetName(this ProductTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this ProductTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ProductTypeEnum, string> Names = new()
    {
        { ProductTypeEnum.Standard, "Padrão" },
        { ProductTypeEnum.Apparel, "Vestuário" },
        { ProductTypeEnum.Perishable, "Perecível" },
        { ProductTypeEnum.Bulk, "Granel" },
    };

    public static readonly Dictionary<ProductTypeEnum, string> ApiNames = new()
    {
        { ProductTypeEnum.Standard, "Standard" },
        { ProductTypeEnum.Apparel, "Apparel" },
        { ProductTypeEnum.Perishable, "Perishable" },
        { ProductTypeEnum.Bulk, "Bulk" },
    };
}
