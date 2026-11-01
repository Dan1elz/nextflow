namespace Nextflow.Domain.Enums;

/// <summary>Ciclo de vida do produto: fora de linha nunca recebe recomendação de compra (RN-CE19).</summary>
public enum LifecycleEnum : byte
{
    Active = 1,
    Discontinued = 2,
}

public static class LifecycleEnumExtensions
{
    public static string GetName(this LifecycleEnum @enum) => Names[@enum];
    public static string GetApiName(this LifecycleEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<LifecycleEnum, string> Names = new()
    {
        { LifecycleEnum.Active, "Em linha" },
        { LifecycleEnum.Discontinued, "Fora de linha" },
    };

    public static readonly Dictionary<LifecycleEnum, string> ApiNames = new()
    {
        { LifecycleEnum.Active, "Active" },
        { LifecycleEnum.Discontinued, "Discontinued" },
    };
}
