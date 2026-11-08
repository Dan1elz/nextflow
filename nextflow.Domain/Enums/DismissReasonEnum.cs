namespace Nextflow.Domain.Enums;

/// <summary>Motivo de descartar uma recomendação (T-DF8). Opcional; "Outro" pede o texto.</summary>
public enum DismissReasonEnum : byte
{
    BoughtElsewhere = 1,
    DisagreeQuantity = 2,
    WillDiscontinue = 3,
    Other = 4,
}

public static class DismissReasonEnumExtensions
{
    public static string GetName(this DismissReasonEnum @enum) => Names[@enum];
    public static string GetApiName(this DismissReasonEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<DismissReasonEnum, string> Names = new()
    {
        { DismissReasonEnum.BoughtElsewhere, "Já comprei por fora" },
        { DismissReasonEnum.DisagreeQuantity, "Discordo da quantidade" },
        { DismissReasonEnum.WillDiscontinue, "Produto vai sair de linha" },
        { DismissReasonEnum.Other, "Outro" },
    };

    public static readonly Dictionary<DismissReasonEnum, string> ApiNames = new()
    {
        { DismissReasonEnum.BoughtElsewhere, "BoughtElsewhere" },
        { DismissReasonEnum.DisagreeQuantity, "DisagreeQuantity" },
        { DismissReasonEnum.WillDiscontinue, "WillDiscontinue" },
        { DismissReasonEnum.Other, "Other" },
    };
}
