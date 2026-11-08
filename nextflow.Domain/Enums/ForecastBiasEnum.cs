namespace Nextflow.Domain.Enums;

/// <summary>Tendência da previsão de um produto nos meses medidos (T-DF10): subestima, superestima ou equilibrada.</summary>
public enum ForecastBiasEnum : byte
{
    Under = 1,
    Over = 2,
    Balanced = 3,
}

public static class ForecastBiasEnumExtensions
{
    public static string GetName(this ForecastBiasEnum @enum) => Names[@enum];
    public static string GetApiName(this ForecastBiasEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ForecastBiasEnum, string> Names = new()
    {
        { ForecastBiasEnum.Under, "Subestima" },
        { ForecastBiasEnum.Over, "Superestima" },
        { ForecastBiasEnum.Balanced, "Equilibrada" },
    };

    public static readonly Dictionary<ForecastBiasEnum, string> ApiNames = new()
    {
        { ForecastBiasEnum.Under, "Under" },
        { ForecastBiasEnum.Over, "Over" },
        { ForecastBiasEnum.Balanced, "Balanced" },
    };
}
