namespace Nextflow.Domain.Enums;

/// <summary>De onde veio o prazo de entrega usado no cálculo: média real dos recebimentos, vínculo produto × fornecedor ou cadastro do fornecedor.</summary>
public enum LeadTimeSourceEnum : byte
{
    History = 1,
    ProductSupplier = 2,
    Supplier = 3,
}

public static class LeadTimeSourceEnumExtensions
{
    public static string GetName(this LeadTimeSourceEnum @enum) => Names[@enum];
    public static string GetApiName(this LeadTimeSourceEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<LeadTimeSourceEnum, string> Names = new()
    {
        { LeadTimeSourceEnum.History, "Média dos recebimentos" },
        { LeadTimeSourceEnum.ProductSupplier, "Vínculo do produto" },
        { LeadTimeSourceEnum.Supplier, "Cadastro do fornecedor" },
    };

    public static readonly Dictionary<LeadTimeSourceEnum, string> ApiNames = new()
    {
        { LeadTimeSourceEnum.History, "History" },
        { LeadTimeSourceEnum.ProductSupplier, "ProductSupplier" },
        { LeadTimeSourceEnum.Supplier, "Supplier" },
    };
}
