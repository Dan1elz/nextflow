namespace Nextflow.Domain.Enums;

/// <summary>Situação da recomendação: nova análise expira as pendentes da anterior (RN-DF14).</summary>
public enum RecommendationStatusEnum : byte
{
    Pending = 1,
    Applied = 2,
    Dismissed = 3,
    Expired = 4,
}

public static class RecommendationStatusEnumExtensions
{
    public static string GetName(this RecommendationStatusEnum @enum) => Names[@enum];
    public static string GetApiName(this RecommendationStatusEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<RecommendationStatusEnum, string> Names = new()
    {
        { RecommendationStatusEnum.Pending, "Pendente" },
        { RecommendationStatusEnum.Applied, "Aplicada" },
        { RecommendationStatusEnum.Dismissed, "Descartada" },
        { RecommendationStatusEnum.Expired, "Expirada" },
    };

    public static readonly Dictionary<RecommendationStatusEnum, string> ApiNames = new()
    {
        { RecommendationStatusEnum.Pending, "Pending" },
        { RecommendationStatusEnum.Applied, "Applied" },
        { RecommendationStatusEnum.Dismissed, "Dismissed" },
        { RecommendationStatusEnum.Expired, "Expired" },
    };
}
