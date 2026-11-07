using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Models;

/// <summary>Quantidade de compra de uma variação, rateada pela proporção histórica de venda da grade (RN-DF21, L-DF5). Editável na aplicação (F3.4).</summary>
public class ForecastRecommendationItem : BaseModel
{
    public Guid RecommendationId { get; private set; }
    public Guid VariantId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal SharePercent { get; private set; }

    public virtual ForecastRecommendation Recommendation { get; private set; } = null!;
    public virtual ProductVariant Variant { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "item da recomendação";
    public override string Plural => "itens da recomendação";

    private ForecastRecommendationItem() { }

    public ForecastRecommendationItem(Guid recommendationId, NewForecastRecommendationItemDto dto)
    {
        RecommendationId = recommendationId;
        VariantId = dto.VariantId;
        Quantity = dto.Quantity;
        SharePercent = dto.SharePercent;
    }
}
