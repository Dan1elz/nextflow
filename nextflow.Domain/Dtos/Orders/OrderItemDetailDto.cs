using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>
/// Item do detalhe do pedido (T-VD5): preço de tabela, preço cobrado, desconto e total gravados na venda. Margem sobre o
/// preço cobrado líquido, com o custo médio do momento; nula sem custo. Lote entra na F1.7.
/// </summary>
public class OrderItemDetailDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid VariantId { get; set; }
    /// <summary>Nulo = variação padrão oculta.</summary>
    public string? VariantLabel { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public short DecimalPlaces { get; set; }
    public decimal ListPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public OrderItemPromotionDto? Promotion { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? MarginPercent { get; set; }

    public OrderItemDetailDto() { }

    public OrderItemDetailDto(OrderItem entity)
    {
        Id = entity.Id;
        VariantId = entity.VariantId;
        if (entity.Variant != null)
        {
            var label = ProductVariantResponseDto.LabelOf(entity.Variant);
            VariantLabel = string.IsNullOrEmpty(label) ? null : label;
            Sku = entity.Variant.Sku;
            ProductId = entity.Variant.ProductId;
            if (entity.Variant.Product != null)
            {
                ProductName = entity.Variant.Product.Name;
                SaleUnit = entity.Variant.Product.SaleUnit.GetApiName();
                DecimalPlaces = entity.Variant.Product.DecimalPlaces;
            }
        }

        Quantity = entity.Quantity;
        ListPrice = entity.ListPrice;
        UnitPrice = entity.UnitPrice;
        Discount = entity.Discount;
        LineTotal = entity.LineTotal;
        if (entity.Promotion != null)
            Promotion = new OrderItemPromotionDto { Id = entity.Promotion.Id, DiscountPercent = entity.Promotion.DiscountPercent };
        UnitCost = entity.UnitCost;
        MarginPercent = entity.UnitCost is { } cost && entity.LineTotal > 0
            ? Math.Round((entity.LineTotal - cost * entity.Quantity) / entity.LineTotal * 100, 2, MidpointRounding.AwayFromZero)
            : null;
    }
}
