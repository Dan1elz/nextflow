using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Suppliers;

/// <summary>
/// Linha da aba Produtos do fornecedor (T-CD2): o vínculo produto × fornecedor visto pelo lado do fornecedor.
/// <see cref="LeadTimeDays"/> é o efetivo, o do vínculo ou o do fornecedor (RN-CE60); <see cref="LastCost"/> vem do último
/// recebimento (F2.8).
/// </summary>
public class SupplierProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public bool ProductIsActive { get; set; }
    public string Lifecycle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public string? SupplierSku { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    public decimal? LastCost { get; set; }

    public SupplierProductDto() { }

    public SupplierProductDto(ProductSupplier link, Supplier supplier)
    {
        ProductId = link.ProductId;
        ProductName = link.Product.Name;
        Brand = link.Product.Brand;
        ProductIsActive = link.Product.IsActive;
        Lifecycle = link.Product.Lifecycle.GetApiName();
        IsPreferred = link.IsPreferred;
        SupplierSku = link.SupplierSku;
        LeadTimeDays = link.LeadTimeDays ?? supplier.LeadTimeDays;
        MinOrderQty = link.MinOrderQty;
        OrderMultiple = link.OrderMultiple;
        LastCost = link.LastCost;
    }
}
