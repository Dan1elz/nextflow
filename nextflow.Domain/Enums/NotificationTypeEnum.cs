namespace Nextflow.Domain.Enums;

/// <summary>Tipo da notificação do sino (T-DF12): análise com recomendações (RN-DF18), lote vencendo e estoque baixo (resumo diário, L-DF4).</summary>
public enum NotificationTypeEnum : byte
{
    NewRecommendations = 1,
    LotExpiring = 2,
    LowStock = 3,
}

public static class NotificationTypeEnumExtensions
{
    public static string GetName(this NotificationTypeEnum @enum) => Names[@enum];
    public static string GetApiName(this NotificationTypeEnum @enum) => ApiNames[@enum];

    public static readonly Dictionary<NotificationTypeEnum, string> Names = new()
    {
        { NotificationTypeEnum.NewRecommendations, "Novas recomendações" },
        { NotificationTypeEnum.LotExpiring, "Lote vencendo" },
        { NotificationTypeEnum.LowStock, "Estoque baixo" },
    };

    public static readonly Dictionary<NotificationTypeEnum, string> ApiNames = new()
    {
        { NotificationTypeEnum.NewRecommendations, "NewRecommendations" },
        { NotificationTypeEnum.LotExpiring, "LotExpiring" },
        { NotificationTypeEnum.LowStock, "LowStock" },
    };
}
