using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Linha da prévia: preço de tabela, preço efetivo do dia e total da linha, como a venda gravaria.</summary>
public class QuickSalePreviewItemDto
{
    public Guid VariantId { get; set; }
    public decimal ListPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public OrderItemPromotionDto? Promotion { get; set; }

    public QuickSalePreviewItemDto() { }

    public QuickSalePreviewItemDto(OrderItem item, Promotion? promotion)
    {
        VariantId = item.VariantId;
        ListPrice = item.ListPrice;
        UnitPrice = item.UnitPrice;
        Discount = item.Discount;
        LineTotal = item.LineTotal;
        if (promotion != null)
            Promotion = new OrderItemPromotionDto { Id = promotion.Id, DiscountPercent = promotion.DiscountPercent };
    }
}
