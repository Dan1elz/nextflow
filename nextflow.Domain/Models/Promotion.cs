using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Promoção do produto inteiro (<see cref="VariantId"/> nulo) ou de uma variação (T-VD7). Vigente = ativa e hoje entre
/// início e fim, inclusive; cancelar = inativar (RN-VD12). O status é calculado, nunca gravado.
/// </summary>
public class Promotion : BaseModel
{
    public Guid ProductId { get; private set; }
    public Guid? VariantId { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Reason { get; private set; }
    public PromotionSourceEnum Source { get; private set; } = PromotionSourceEnum.Manual;
    /// <summary>Recomendação da previsão que originou a promoção; sem FK até a F3.4.</summary>
    public Guid? ForecastRecommendationId { get; private set; }
    /// <summary>O Admin confirmou o aviso de preço abaixo do custo médio (RN-VD13).</summary>
    public bool BelowCostConfirmed { get; private set; }
    public Guid CreatedById { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? CanceledAt { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual ProductVariant? Variant { get; private set; }
    public virtual User CreatedBy { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "promoção";
    public override string Plural => "promoções";

    private Promotion() { }

    public Promotion(PromotionRequestDto dto, Guid createdById, bool belowCostConfirmed)
    {
        ProductId = dto.ProductId;
        VariantId = dto.VariantId;
        DiscountPercent = dto.DiscountPercent;
        StartDate = dto.StartDate;
        EndDate = dto.EndDate;
        Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        CreatedById = createdById;
        BelowCostConfirmed = belowCostConfirmed;
    }

    /// <summary>Promoção aplicada de uma recomendação "Promover" da previsão (T-DF6, F3.4).</summary>
    public Promotion(PromotionRequestDto dto, Guid forecastRecommendationId, Guid createdById, bool belowCostConfirmed)
        : this(dto, createdById, belowCostConfirmed)
    {
        Source = PromotionSourceEnum.AiRecommendation;
        ForecastRecommendationId = forecastRecommendationId;
    }

    public void Cancel()
    {
        IsActive = false;
        CanceledAt = DateTime.UtcNow;
    }

    public bool AppliesTo(Guid variantId) => VariantId == null || VariantId == variantId;

    public bool IsCurrentOn(DateOnly today) => IsActive && StartDate <= today && today <= EndDate;

    public PromotionStatusEnum StatusOn(DateOnly today)
        => !IsActive ? PromotionStatusEnum.Canceled
            : today < StartDate ? PromotionStatusEnum.Scheduled
            : today <= EndDate ? PromotionStatusEnum.Active
            : PromotionStatusEnum.Ended;
}
