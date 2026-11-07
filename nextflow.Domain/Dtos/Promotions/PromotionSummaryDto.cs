using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Promotions;

/// <summary>Promoção vigente mostrada em outras telas (seletor de venda, variações do produto).</summary>
public class PromotionSummaryDto
{
    public Guid Id { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly EndDate { get; set; }

    public PromotionSummaryDto() { }

    public PromotionSummaryDto(Promotion entity)
    {
        Id = entity.Id;
        DiscountPercent = entity.DiscountPercent;
        EndDate = entity.EndDate;
    }
}
