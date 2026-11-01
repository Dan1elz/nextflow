using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Suppliers;

/// <summary>
/// Linha da listagem (T-CD1). <see cref="RealLeadTimeDays"/> e <see cref="LastPurchaseOn"/> dependem de compras: ficam
/// nulos até a fase 2. <see cref="ProductCount"/> conta os produtos ativos vinculados.
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
        ProductCount = entity.Products.Count;
        IsActive = entity.IsActive;
    }
}
