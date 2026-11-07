using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Dtos;

/// <summary>
/// Criação de promoção. Os campos <see cref="Source"/>, <see cref="ForecastRecommendationId"/> e
/// <see cref="CreatedByUserId"/> são preenchidos pelo servidor (controller ou caso de uso de recomendações),
/// nunca pelo corpo da requisição HTTP (a API recebe <c>PromotionRequestDto</c>).
/// </summary>
public class CreatePromotionDto : BaseDto
{
    [NotEmptyGuid(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "O percentual de desconto é obrigatório."), Range(1, 90, ErrorMessage = "O percentual de desconto deve estar entre 1% e 90%.")]
    public decimal DiscountPercent { get; set; }

    [Required(ErrorMessage = "A data de início é obrigatória.")]
    public DateOnly StartDate { get; set; }

    [Required(ErrorMessage = "A data de término é obrigatória.")]
    public DateOnly EndDate { get; set; }

    [StringLength(255, ErrorMessage = "O motivo não pode exceder 255 caracteres.")]
    public string? Reason { get; set; }

    [JsonIgnore]
    public PromotionSource Source { get; set; } = PromotionSource.Manual;

    [JsonIgnore]
    public Guid? ForecastRecommendationId { get; set; }

    [JsonIgnore]
    [NotEmptyGuid(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid CreatedByUserId { get; set; }
}

public class PromotionSummaryDto
{
    public Guid Id { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal PromotionalPrice { get; set; }

    public PromotionSummaryDto() { }

    public PromotionSummaryDto(Promotion entity, decimal price)
    {
        Id = entity.Id;
        DiscountPercent = entity.DiscountPercent;
        StartDate = entity.StartDate;
        EndDate = entity.EndDate;
        PromotionalPrice = entity.CalculatePromotionalPrice(price);
    }
}

public class PromotionResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public ProductResponseDto? Product { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    public PromotionSource Source { get; set; }
    public Guid? ForecastRecommendationId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public bool IsActive { get; set; }

    /// <summary>active | scheduled | ended | canceled (calculado com a data de negócio atual).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Preço atual do produto (sem promoção).</summary>
    public decimal OriginalPrice { get; set; }

    /// <summary>Preço atual do produto com o desconto desta promoção.</summary>
    public decimal PromotionalPrice { get; set; }

    public DateTime CreateAt { get; set; }

    public PromotionResponseDto() { }

    public PromotionResponseDto(Promotion entity)
    {
        Id = entity.Id;
        ProductId = entity.ProductId;
        Product = entity.Product != null ? new ProductResponseDto(entity.Product) : null;
        DiscountPercent = entity.DiscountPercent;
        StartDate = entity.StartDate;
        EndDate = entity.EndDate;
        Reason = entity.Reason;
        Source = entity.Source;
        ForecastRecommendationId = entity.ForecastRecommendationId;
        CreatedByUserId = entity.CreatedByUserId;
        IsActive = entity.IsActive;
        Status = entity.GetStatus(BusinessClock.Today);
        OriginalPrice = entity.Product?.Price ?? 0;
        PromotionalPrice = entity.CalculatePromotionalPrice(OriginalPrice);
        CreateAt = entity.CreateAt;
    }
}
