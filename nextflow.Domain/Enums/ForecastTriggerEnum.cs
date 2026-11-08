namespace Nextflow.Domain.Enums;

/// <summary>Origem da análise: botão manual ou agendamento semanal (RN-DF18).</summary>
public enum ForecastTriggerEnum : byte
{
    Manual = 1,
    Scheduled = 2,
}

public static class ForecastTriggerEnumExtensions
{
    public static string GetName(this ForecastTriggerEnum @enum) => Names[@enum];
    public static string GetApiName(this ForecastTriggerEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<ForecastTriggerEnum, string> Names = new()
    {
        { ForecastTriggerEnum.Manual, "Manual" },
        { ForecastTriggerEnum.Scheduled, "Semanal" },
    };

    public static readonly Dictionary<ForecastTriggerEnum, string> ApiNames = new()
    {
        { ForecastTriggerEnum.Manual, "Manual" },
        { ForecastTriggerEnum.Scheduled, "Scheduled" },
    };
}
