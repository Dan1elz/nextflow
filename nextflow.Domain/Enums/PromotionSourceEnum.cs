namespace Nextflow.Domain.Enums;

/// <summary>Origem da promoção: manual (T-VD7) ou aplicada de uma recomendação da previsão (F3.4).</summary>
public enum PromotionSourceEnum : byte
{
    Manual = 1,
    AiRecommendation = 2,
}

public static class PromotionSourceEnumExtensions
{
    public static string GetName(this PromotionSourceEnum @enum) => Names[@enum];
    public static string GetApiName(this PromotionSourceEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<PromotionSourceEnum, string> Names = new()
    {
        { PromotionSourceEnum.Manual, "Manual" },
        { PromotionSourceEnum.AiRecommendation, "Recomendação da IA" },
    };

    public static readonly Dictionary<PromotionSourceEnum, string> ApiNames = new()
    {
        { PromotionSourceEnum.Manual, "Manual" },
        { PromotionSourceEnum.AiRecommendation, "AiRecommendation" },
    };
}
