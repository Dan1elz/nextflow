namespace Nextflow.Domain.Models;

/// <summary>
/// Quanto da compra recomendada vai para cada loja (RN-DF27), calculado pela análise: participação da loja nas vendas, saldo, a
/// caminho e a falta no horizonte. O "Comprar" já abre dividido assim.
/// </summary>
public class ForecastRecommendationStore : BaseModel
{
    public Guid RecommendationId { get; private set; }
    public Guid StoreId { get; private set; }
    public decimal SharePercent { get; private set; }
    public decimal Shortfall { get; private set; }
    public decimal Quantity { get; private set; }

    public virtual ForecastRecommendation Recommendation { get; private set; } = null!;
    public virtual Store Store { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "parte da loja";
    public override string Plural => "partes das lojas";

    private ForecastRecommendationStore() { }

    public ForecastRecommendationStore(Guid recommendationId, Guid storeId, decimal sharePercent, decimal shortfall, decimal quantity)
    {
        RecommendationId = recommendationId;
        StoreId = storeId;
        SharePercent = sharePercent;
        Shortfall = shortfall;
        Quantity = quantity;
    }
}
