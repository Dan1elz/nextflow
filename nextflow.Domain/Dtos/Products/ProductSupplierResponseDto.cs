using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>
/// Linha da aba Fornecedores (T3). <see cref="LeadTimeDays"/> é o efetivo: o do vínculo ou, sem ele, o do fornecedor (RN-CE60).
/// Provisório: <see cref="LastCost"/> e <see cref="LastPurchaseOn"/> ficam nulos até o recebimento de compra (F2.8).
/// </summary>
public class ProductSupplierResponseDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public bool SupplierIsActive { get; set; }
    public bool IsPreferred { get; set; }
    public string? SupplierSku { get; set; }
    public int LeadTimeDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
    public decimal? LastCost { get; set; }
    public DateOnly? LastPurchaseOn { get; set; }

    public ProductSupplierResponseDto() { }

    public ProductSupplierResponseDto(ProductSupplier entity)
    {
        SupplierId = entity.SupplierId;
        SupplierName = entity.Supplier != null ? entity.Supplier.Name : string.Empty;
        SupplierIsActive = entity.Supplier != null && entity.Supplier.IsActive;
        IsPreferred = entity.IsPreferred;
        SupplierSku = entity.SupplierSku;
        LeadTimeDays = entity.LeadTimeDays ?? (entity.Supplier != null ? entity.Supplier.LeadTimeDays : 0);
        MinOrderQty = entity.MinOrderQty;
        OrderMultiple = entity.OrderMultiple;
    }
}
