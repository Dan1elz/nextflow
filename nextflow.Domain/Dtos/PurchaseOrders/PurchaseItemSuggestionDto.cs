using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>
/// O que a linha do pedido mostra ao escolher a variação (T-CP2): vínculo com o fornecedor (RN-CE27, RN-CE30), custo médio
/// (RN-CE31), disponível na loja e a caminho (RN-CP05). O último custo pago ao fornecedor fica nulo até a F2.8.
/// </summary>
public class PurchaseItemSuggestionDto
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? SupplierSku { get; set; }
    public SaleUnitEnum SaleUnit { get; set; }
    public short DecimalPlaces { get; set; }
    public bool TracksLots { get; set; }
    public LifecycleEnum Lifecycle { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    public decimal? LastCostFromSupplier { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal AvailableAtStore { get; set; }
    public decimal IncomingQuantity { get; set; }

    public PurchaseItemSuggestionDto() { }

    /// <param name="variant">Com produto, vínculos de fornecedor e valores da grade carregados.</param>
    public PurchaseItemSuggestionDto(ProductVariant variant, Guid supplierId, decimal availableAtStore, decimal incomingQuantity)
    {
        var product = variant.Product;
        var link = product?.Suppliers.FirstOrDefault(s => s.SupplierId == supplierId);
        VariantId = variant.Id;
        ProductId = variant.ProductId;
        ProductName = product != null ? product.Name : string.Empty;
        VariantLabel = ProductVariantResponseDto.LabelOf(variant);
        Sku = variant.Sku;
        SupplierSku = link?.SupplierSku;
        SaleUnit = product?.SaleUnit ?? SaleUnitEnum.Unit;
        DecimalPlaces = product?.DecimalPlaces ?? 0;
        TracksLots = product?.TracksLots ?? false;
        Lifecycle = product?.Lifecycle ?? LifecycleEnum.Active;
        MinOrderQty = link?.MinOrderQty;
        OrderMultiple = link?.OrderMultiple;
        AverageCost = variant.AverageCost;
        AvailableAtStore = availableAtStore;
        IncomingQuantity = incomingQuantity;
    }
}
