using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Resultado da busca de itens do pedido (T-CP2): <see cref="IsLinked"/> diz se o produto é do fornecedor.</summary>
public class PurchaseVariantOptionDto
{
    public Guid VariantId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string VariantLabel { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public bool IsLinked { get; set; }

    public PurchaseVariantOptionDto() { }

    /// <param name="variant">Com produto, vínculos de fornecedor e valores da grade carregados.</param>
    public PurchaseVariantOptionDto(ProductVariant variant, Guid supplierId)
    {
        VariantId = variant.Id;
        ProductId = variant.ProductId;
        ProductName = variant.Product != null ? variant.Product.Name : string.Empty;
        VariantLabel = ProductVariantResponseDto.LabelOf(variant);
        Sku = variant.Sku;
        IsLinked = variant.Product != null && variant.Product.Suppliers.Any(s => s.SupplierId == supplierId);
    }
}
