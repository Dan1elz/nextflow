namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Item do bloco "Lotes vencendo" (T-DF1).</summary>
public class ExpiringLotItemDto
{
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantLabel { get; set; }
    public Guid LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    /// <summary>Loja do local: o "ver mais" do painel troca a loja do topo para agir (comprar, promover, baixa).</summary>
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public int DaysToExpire { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Recomendação pendente (não "Manter") da previsão para o produto, para agir dali com a sugestão da IA; nula sem uma.</summary>
    public Guid? RecommendationId { get; set; }
    public string? RecommendationAction { get; set; }
}
