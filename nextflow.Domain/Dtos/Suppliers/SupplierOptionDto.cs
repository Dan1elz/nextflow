using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Suppliers;

/// <summary>Fornecedor no formulário do produto: o prazo é o padrão do campo vazio e o status marca o inativo já vinculado.</summary>
public class SupplierOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public bool IsActive { get; set; }

    public SupplierOptionDto() { }

    public SupplierOptionDto(Supplier entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        LeadTimeDays = entity.LeadTimeDays;
        IsActive = entity.IsActive;
    }
}
