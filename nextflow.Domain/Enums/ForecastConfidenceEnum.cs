namespace Nextflow.Domain.Enums;

/// <summary>Confiança do cálculo pelos meses de histórico (RN-DF05). A IA nunca a altera (RN-DF10).</summary>
public enum ForecastConfidenceEnum : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
}

public static class ForecastConfidenceEnumExtensions
{
    public static string GetName(this ForecastConfidenceEnum @enum) => Names[@enum];
    public static string GetApiName(this ForecastConfidenceEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ForecastConfidenceEnum, string> Names = new()
    {
        { ForecastConfidenceEnum.Low, "Baixa" },
        { ForecastConfidenceEnum.Medium, "Média" },
        { ForecastConfidenceEnum.High, "Alta" },
    };

    public static readonly Dictionary<ForecastConfidenceEnum, string> ApiNames = new()
    {
        { ForecastConfidenceEnum.Low, "Low" },
        { ForecastConfidenceEnum.Medium, "Medium" },
        { ForecastConfidenceEnum.High, "High" },
    };
}
