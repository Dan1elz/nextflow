using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("promotions")]
public class Promotion : BaseModel, IDeletable
{
    public const string StatusActive = "active";
    public const string StatusScheduled = "scheduled";
    public const string StatusEnded = "ended";
    public const string StatusCanceled = "canceled";

    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    [Required(ErrorMessage = "O percentual de desconto é obrigatório."), Range(1, 90, ErrorMessage = "O percentual de desconto deve estar entre 1% e 90%.")]
    public decimal DiscountPercent { get; private set; }

    [Required(ErrorMessage = "A data de início é obrigatória.")]
    public DateOnly StartDate { get; private set; }

    [Required(ErrorMessage = "A data de término é obrigatória.")]
    public DateOnly EndDate { get; private set; }

    [StringLength(255, ErrorMessage = "O motivo não pode exceder 255 caracteres.")]
    public string? Reason { get; private set; }

    [Required(ErrorMessage = "A origem da promoção é obrigatória.")]
    public PromotionSource Source { get; private set; } = PromotionSource.Manual;

    public Guid? ForecastRecommendationId { get; private set; }

    [ForeignKey("users"), Required(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid CreatedByUserId { get; private set; }
    public virtual User? CreatedByUser { get; private set; }

    public bool IsActive { get; set; } = true;
    public DateTime? UpdateAt { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "promoção";
    public override string Plural => "promoções";

    private Promotion() : base() { }

    public Promotion(CreatePromotionDto dto) : base()
    {
        ProductId = dto.ProductId;
        DiscountPercent = dto.DiscountPercent;
        StartDate = dto.StartDate;
        EndDate = dto.EndDate;
        Reason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        Source = dto.Source;
        ForecastRecommendationId = dto.ForecastRecommendationId;
        CreatedByUserId = dto.CreatedByUserId;
    }

    public void Delete()
    {
        IsActive = false;
        UpdateAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateAt = DateTime.UtcNow;
        }
    }

    /// <summary>Vigente = ativa e <paramref name="date"/> dentro do período [StartDate, EndDate].</summary>
    public bool IsCurrent(DateOnly date) => IsActive && StartDate <= date && date <= EndDate;

    /// <summary>Status calculado: active | scheduled | ended | canceled.</summary>
    public string GetStatus(DateOnly today)
    {
        if (!IsActive) return StatusCanceled;
        if (today < StartDate) return StatusScheduled;
        if (today > EndDate) return StatusEnded;
        return StatusActive;
    }

    /// <summary>Preço com o desconto aplicado (arredondado em 2 casas).</summary>
    public decimal CalculatePromotionalPrice(decimal price)
        => Math.Round(price * (1 - DiscountPercent / 100m), 2, MidpointRounding.AwayFromZero);
}
