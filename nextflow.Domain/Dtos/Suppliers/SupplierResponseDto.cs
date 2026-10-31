using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Suppliers;

/// <summary>Detalhe (T-CD2). <see cref="RealLeadTimeDays"/> depende de compras: fica nulo até a fase 2.</summary>
public class SupplierResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public int? RealLeadTimeDays { get; set; }
    public AddressResponseDto? MainAddress { get; set; }
    public ContactResponseDto? MainContact { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SupplierResponseDto() { }

    public SupplierResponseDto(Supplier entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Cnpj = entity.Cnpj.Value;
        LeadTimeDays = entity.LeadTimeDays;
        Note = entity.Note;
        IsActive = entity.IsActive;
        CreatedAt = entity.CreatedAt;
        UpdatedAt = entity.UpdatedAt;
    }
}
