using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Item no detalhe (T-CP3). Custo médio após a entrada vem do recebimento (F2.8); lotes entram com a F1.7.</summary>
public class PurchaseOrderDetailItemDto
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? SupplierSku { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal Subtotal { get; set; }
    public bool TracksLots { get; set; }
    public List<PurchaseItemLotDto>? Lots { get; set; }
    public decimal? AverageCostAfter { get; set; }

    public PurchaseOrderDetailItemDto() { }

    /// <param name="entity">Com variação, produto, vínculos e valores da grade carregados.</param>
    public PurchaseOrderDetailItemDto(PurchaseItem entity, Guid supplierId)
    {
        var variant = entity.Variant;
        var product = variant?.Product;
        Id = entity.Id;
        VariantId = entity.VariantId;
        ProductId = variant?.ProductId ?? Guid.Empty;
        ProductName = product != null ? product.Name : string.Empty;
        VariantLabel = variant != null ? ProductVariantResponseDto.LabelOf(variant) : string.Empty;
        Sku = variant != null ? variant.Sku : string.Empty;
        SupplierSku = product?.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId)?.SupplierSku;
        Quantity = entity.Quantity;
        UnitCost = entity.UnitCost;
        Subtotal = entity.Subtotal;
        TracksLots = product?.TracksLots ?? false;
        AverageCostAfter = entity.AverageCostAfter;
    }
}
