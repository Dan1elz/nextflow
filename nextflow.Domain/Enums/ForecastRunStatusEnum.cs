namespace Nextflow.Domain.Enums;

/// <summary>Status da análise de previsão (RN-DF13, RN-DF23): em andamento, concluída com IA, concluída só com o cálculo ou falha.</summary>
public enum ForecastRunStatusEnum : byte
{
    Running = 1,
    Completed = 2,
    CompletedWithoutAi = 3,
    Failed = 4,
}

public static class ForecastRunStatusEnumExtensions
{
    public static string GetName(this ForecastRunStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this ForecastRunStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ForecastRunStatusEnum, string> Names = new()
    {
        { ForecastRunStatusEnum.Running, "Em andamento" },
        { ForecastRunStatusEnum.Completed, "Concluída" },
        { ForecastRunStatusEnum.CompletedWithoutAi, "Concluída sem IA" },
        { ForecastRunStatusEnum.Failed, "Falhou" },
    };

    public static readonly Dictionary<ForecastRunStatusEnum, string> ApiNames = new()
    {
        { ForecastRunStatusEnum.Running, "Running" },
        { ForecastRunStatusEnum.Completed, "Completed" },
        { ForecastRunStatusEnum.CompletedWithoutAi, "CompletedWithoutAi" },
        { ForecastRunStatusEnum.Failed, "Failed" },
    };
}
