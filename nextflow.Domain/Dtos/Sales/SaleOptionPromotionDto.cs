namespace Nextflow.Domain.Dtos.Sales;

/// <summary>Promoção vigente da opção de venda (T-VD1). Preenchida a partir da F2.3; até lá a opção vem sem promoção.</summary>
public class SaleOptionPromotionDto
{
    public Guid Id { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly EndDate { get; set; }
}
