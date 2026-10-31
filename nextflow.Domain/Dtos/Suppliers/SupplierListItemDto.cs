using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Suppliers;

/// <summary>
/// Linha da listagem (T-CD1). <see cref="RealLeadTimeDays"/>, <see cref="ProductCount"/> e <see cref="LastPurchaseOn"/>
/// dependem de compras e produtos: ficam nulos ou zero até as fases 1 e 2.
/// </summary>
public class SupplierListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string? CityState { get; set; }
    public string? MainContact { get; set; }
    public int LeadTimeDays { get; set; }
    public int? RealLeadTimeDays { get; set; }
    public int ProductCount { get; set; }
    public DateOnly? LastPurchaseOn { get; set; }
    public bool IsActive { get; set; }

    public SupplierListItemDto() { }

    public SupplierListItemDto(Supplier entity, OwnerSummaryDto? summary)
    {
        Id = entity.Id;
        Name = entity.Name;
        Cnpj = entity.Cnpj.Value;
        CityState = summary?.CityState;
        MainContact = summary?.MainContact;
        LeadTimeDays = entity.LeadTimeDays;
        IsActive = entity.IsActive;
    }
}
