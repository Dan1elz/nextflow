namespace Nextflow.Domain.Enums;

/// <summary>Unidade de venda: compra e venda são sempre nela, sem conversão (RN-CE30).</summary>
public enum SaleUnitEnum : byte
{
    Unit = 1,
    Kilogram = 2,
    Liter = 3,
    Meter = 4,
}

public static class SaleUnitEnumExtensions
{
    public static string GetName(this SaleUnitEnum @enum) => Names[@enum];
    public static string GetApiName(this SaleUnitEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<SaleUnitEnum, string> Names = new()
    {
        { SaleUnitEnum.Unit, "Unidade" },
        { SaleUnitEnum.Kilogram, "Quilograma" },
        { SaleUnitEnum.Liter, "Litro" },
        { SaleUnitEnum.Meter, "Metro" },
    };

    public static readonly Dictionary<SaleUnitEnum, string> ApiNames = new()
    {
        { SaleUnitEnum.Unit, "Unit" },
        { SaleUnitEnum.Kilogram, "Kilogram" },
        { SaleUnitEnum.Liter, "Liter" },
        { SaleUnitEnum.Meter, "Meter" },
    };
}
