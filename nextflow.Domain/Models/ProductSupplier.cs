using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Models;

/// <summary>Vínculo produto × fornecedor (RN-CE27, RN-CE30). Prazo nulo usa o do fornecedor (RN-CE60).</summary>
public class ProductSupplier : BaseModel
{
    public Guid ProductId { get; private set; }
    public Guid SupplierId { get; private set; }
    public bool IsPreferred { get; private set; }
    public string? SupplierSku { get; private set; }
    public int? LeadTimeDays { get; private set; }
    public decimal? MinOrderQty { get; private set; }
    public decimal? OrderMultiple { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual Supplier Supplier { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "vínculo de fornecedor";
    public override string Plural => "vínculos de fornecedor";

    private ProductSupplier() { }

    public ProductSupplier(ProductSupplierRequestDto dto, Guid productId)
    {
        ProductId = productId;
        SupplierId = dto.SupplierId;
        Apply(dto);
    }

    public void Update(ProductSupplierRequestDto dto)
    {
        Apply(dto);
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(ProductSupplierRequestDto dto)
    {
        IsPreferred = dto.IsPreferred;
        SupplierSku = string.IsNullOrWhiteSpace(dto.SupplierSku) ? null : dto.SupplierSku.Trim();
        LeadTimeDays = dto.LeadTimeDays;
        MinOrderQty = dto.MinOrderQty;
        OrderMultiple = dto.OrderMultiple;
    }
}
