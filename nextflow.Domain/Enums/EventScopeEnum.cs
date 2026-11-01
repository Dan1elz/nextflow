namespace Nextflow.Domain.Enums;

/// <summary>Escopo do evento de calendário (RN-CE22, RN-CE42): nacional vem do seed; da empresa, do cadastro.</summary>
public enum EventScopeEnum : byte
{
    National = 1,
    Company = 2,
}

public static class EventScopeEnumExtensions
{
    public static string GetName(this EventScopeEnum @enum) => Names[@enum];
    public static string GetApiName(this EventScopeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<EventScopeEnum, string> Names = new()
    {
        { EventScopeEnum.National, "Nacional" },
        { EventScopeEnum.Company, "Da empresa" },
    };

    public static readonly Dictionary<EventScopeEnum, string> ApiNames = new()
    {
        { EventScopeEnum.National, "National" },
        { EventScopeEnum.Company, "Company" },
    };
}
