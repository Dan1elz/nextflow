namespace Nextflow.Domain.Enums;

/// <summary>Prioridade da recomendação, base do cálculo (cobertura × prazo de entrega) e ajustável pela IA.</summary>
public enum RecommendationPriorityEnum : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
}

public static class RecommendationPriorityEnumExtensions
{
    public static string GetName(this RecommendationPriorityEnum @enum) => Names[@enum];
    public static string GetApiName(this RecommendationPriorityEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<RecommendationPriorityEnum, string> Names = new()
    {
        { RecommendationPriorityEnum.Low, "Baixa" },
        { RecommendationPriorityEnum.Medium, "Média" },
        { RecommendationPriorityEnum.High, "Alta" },
    };

    public static readonly Dictionary<RecommendationPriorityEnum, string> ApiNames = new()
    {
        { RecommendationPriorityEnum.Low, "Low" },
        { RecommendationPriorityEnum.Medium, "Medium" },
        { RecommendationPriorityEnum.High, "High" },
    };
}
