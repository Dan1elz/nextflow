using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Sales;

/// <summary>
/// Opção do seletor de venda (T-VD1), uma por variação. <see cref="EffectivePrice"/> vem do <c>IProductPricingService</c>
/// (RN-VD02). <see cref="AvailableQuantity"/> é o disponível no local de venda (RN-CE08): enquanto não houver orçamento
/// (F2.5), é o saldo físico. <see cref="ActivePromotion"/> é a promoção vigente (F2.3); <see cref="NextLot"/> entra na F1.7.
/// </summary>
public class VariantSaleOptionDto
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    /// <summary>Nulo = variação padrão oculta (produto sem grade).</summary>
    public string? VariantLabel { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? ImageUrl { get; set; }
    public string SaleUnit { get; set; } = string.Empty;
    public short DecimalPlaces { get; set; }
    public bool AllowsNegative { get; set; }
    public decimal ListPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public PromotionSummaryDto? ActivePromotion { get; set; }
    public decimal AvailableQuantity { get; set; }
    /// <summary>Local de venda resolvido (o informado ou o padrão da loja): de onde a venda baixa e o que o aviso de falta exclui.</summary>
    public Guid LocationId { get; set; }
    public SaleOptionLotDto? NextLot { get; set; }
    public string Lifecycle { get; set; } = string.Empty;
    /// <summary>Variação inativa ainda pode ser vendida (RN-CE19 só bloqueia compra); o seletor sinaliza.</summary>
    public bool IsActive { get; set; }

    public VariantSaleOptionDto() { }

    public VariantSaleOptionDto(ProductVariant entity, string? imageUrl, IProductPricingService pricing, Guid locationId, decimal availableQuantity)
    {
        var label = ProductVariantResponseDto.LabelOf(entity);

        VariantId = entity.Id;
        ProductId = entity.ProductId;
        ProductName = entity.Product != null ? entity.Product.Name : string.Empty;
        VariantLabel = string.IsNullOrEmpty(label) ? null : label;
        Sku = entity.Sku;
        Barcode = entity.Barcode;
        ImageUrl = imageUrl;
        ListPrice = entity.Price;
        EffectivePrice = pricing.GetEffectivePrice(entity);
        ActivePromotion = pricing.GetActivePromotion(entity) is { } promotion ? new PromotionSummaryDto(promotion) : null;
        LocationId = locationId;
        AvailableQuantity = availableQuantity;
        IsActive = entity.IsActive;

        if (entity.Product != null)
        {
            SaleUnit = entity.Product.SaleUnit.GetApiName();
            DecimalPlaces = entity.Product.DecimalPlaces;
            AllowsNegative = entity.Product.AllowsNegative;
            Lifecycle = entity.Product.Lifecycle.GetApiName();
        }
    }
}
