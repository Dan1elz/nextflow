namespace Nextflow.Domain.Dtos.Products;

/// <summary>Linha de fornecedor do T2. O vínculo é reconhecido por <see cref="SupplierId"/>, único no produto, e preserva o id ao salvar.</summary>
public class ProductSupplierRequestDto
{
    public Guid SupplierId { get; set; }
    public bool IsPreferred { get; set; }
    public string? SupplierSku { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? OrderMultiple { get; set; }
}
