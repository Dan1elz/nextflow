namespace Nextflow.Domain.Enums;

/// <summary>Como a data do evento é calculada a cada ano (RN-CE41).</summary>
public enum DateRuleEnum : byte
{
    Fixed = 1,
    Easter = 2,
    NthWeekday = 3,
    BlackFriday = 4,
    EasterOffset = 5,
}

public static class DateRuleEnumExtensions
{
    public static string GetName(this DateRuleEnum @enum) => Names[@enum];
    public static string GetApiName(this DateRuleEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<DateRuleEnum, string> Names = new()
    {
        { DateRuleEnum.Fixed, "Data fixa" },
        { DateRuleEnum.Easter, "Páscoa" },
        { DateRuleEnum.NthWeekday, "Enésimo dia da semana" },
        { DateRuleEnum.BlackFriday, "Black Friday" },
        { DateRuleEnum.EasterOffset, "Dias a partir da Páscoa" },
    };

    public static readonly Dictionary<DateRuleEnum, string> ApiNames = new()
    {
        { DateRuleEnum.Fixed, "Fixed" },
        { DateRuleEnum.Easter, "Easter" },
        { DateRuleEnum.NthWeekday, "NthWeekday" },
        { DateRuleEnum.BlackFriday, "BlackFriday" },
        { DateRuleEnum.EasterOffset, "EasterOffset" },
    };
}
