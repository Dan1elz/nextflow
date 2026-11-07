namespace Nextflow.Domain.Enums;

/// <summary>Status da promoção, sempre calculado na leitura pela data de negócio (RN-VD12); não é gravado.</summary>
public enum PromotionStatusEnum : byte
{
    Scheduled = 1,
    Active = 2,
    Ended = 3,
    Canceled = 4,
}

public static class PromotionStatusEnumExtensions
{
    public static string GetName(this PromotionStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this PromotionStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<PromotionStatusEnum, string> Names = new()
    {
        { PromotionStatusEnum.Scheduled, "Agendada" },
        { PromotionStatusEnum.Active, "Vigente" },
        { PromotionStatusEnum.Ended, "Encerrada" },
        { PromotionStatusEnum.Canceled, "Cancelada" },
    };

    public static readonly Dictionary<PromotionStatusEnum, string> ApiNames = new()
    {
        { PromotionStatusEnum.Scheduled, "Scheduled" },
        { PromotionStatusEnum.Active, "Active" },
        { PromotionStatusEnum.Ended, "Ended" },
        { PromotionStatusEnum.Canceled, "Canceled" },
    };
}
