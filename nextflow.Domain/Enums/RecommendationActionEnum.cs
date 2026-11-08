namespace Nextflow.Domain.Enums;

/// <summary>Ação recomendada por produto. Transferir entra na F3.4b; o motor da F3.2 nunca a produz.</summary>
public enum RecommendationActionEnum : byte
{
    Buy = 1,
    Promote = 2,
    Hold = 3,
    Transfer = 4,
}

public static class RecommendationActionEnumExtensions
{
    public static string GetName(this RecommendationActionEnum @enum) => Names[@enum];
    public static string GetApiName(this RecommendationActionEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<RecommendationActionEnum, string> Names = new()
    {
        { RecommendationActionEnum.Buy, "Comprar" },
        { RecommendationActionEnum.Promote, "Promover" },
        { RecommendationActionEnum.Hold, "Manter" },
        { RecommendationActionEnum.Transfer, "Transferir" },
    };

    public static readonly Dictionary<RecommendationActionEnum, string> ApiNames = new()
    {
        { RecommendationActionEnum.Buy, "Buy" },
        { RecommendationActionEnum.Promote, "Promote" },
        { RecommendationActionEnum.Hold, "Hold" },
        { RecommendationActionEnum.Transfer, "Transfer" },
    };
}
