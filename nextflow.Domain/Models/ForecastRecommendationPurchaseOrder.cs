namespace Nextflow.Domain.Models;

/// <summary>
/// Pedido de compra gerado por uma recomendação "Comprar" (RN-DF28): com a compra dividida entre lojas (RN-DF27), uma
/// recomendação gera um pedido por fornecedor × loja.
/// </summary>
public class ForecastRecommendationPurchaseOrder : BaseModel
{
    public Guid RecommendationId { get; private set; }
    public Guid PurchaseOrderId { get; private set; }

    public virtual ForecastRecommendation Recommendation { get; private set; } = null!;
    public virtual PurchaseOrder PurchaseOrder { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "pedido da recomendação";
    public override string Plural => "pedidos da recomendação";

    private ForecastRecommendationPurchaseOrder() { }

    public ForecastRecommendationPurchaseOrder(Guid recommendationId, Guid purchaseOrderId)
    {
        RecommendationId = recommendationId;
        PurchaseOrderId = purchaseOrderId;
    }
}
