namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Promoção aplicada ao item no dia da venda.</summary>
public class OrderItemPromotionDto
{
    public Guid Id { get; set; }
    public decimal DiscountPercent { get; set; }
}
