using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Detalhe da recomendação (T-DF3): a linha da tabela mais o motivo completo da IA e o calculado (sempre identificados, ADR 0005),
/// a quantidade do cálculo antes do ajuste da IA (RN-DF12/22), os números do cálculo, o rateio por variação e a previsão do produto.
/// </summary>
public class ForecastRecommendationDetailDto : ForecastRecommendationListItemDto
{
    /// <summary>Motivo final quando veio da IA; nulo quando é o próprio motivo calculado.</summary>
    public string? ReasonAi { get; set; }
    public string ReasonCalculated { get; set; } = string.Empty;
    public decimal? CalculatedQuantity { get; set; }
    public string? DismissReasonCode { get; set; }
    public string? DismissNote { get; set; }
    public ForecastCalculationDto Calculation { get; set; } = new();
    public List<VariantSplitDto> VariantSplit { get; set; } = [];
    public ProductForecastDto Product { get; set; } = new();

    public ForecastRecommendationDetailDto() { }

    public ForecastRecommendationDetailDto(
        ForecastRecommendation entity, string? imageUrl, ForecastCalculationDto calculation, List<VariantSplitDto> split, ProductForecastDto product)
        : base(entity, imageUrl)
    {
        ReasonAi = entity.Reason != entity.ReasonCalculated ? entity.Reason : null;
        ReasonCalculated = entity.ReasonCalculated;
        CalculatedQuantity = entity.CalculatedQuantity;
        DismissReasonCode = entity.DismissReasonCode?.GetApiName();
        DismissNote = entity.DismissReason;
        Calculation = calculation;
        VariantSplit = split;
        Product = product;
    }
}
