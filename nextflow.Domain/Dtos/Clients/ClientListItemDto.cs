using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Clients;

/// <summary>
/// Linha da listagem (T-CD3). <see cref="OrderCount"/>, <see cref="TotalSpent"/> e <see cref="LastPurchaseOn"/> dependem
/// de pedidos: ficam zero ou nulos até a fase 2.
/// </summary>
public class ClientListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? MainPhone { get; set; }
    public string? CityState { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateOnly? LastPurchaseOn { get; set; }
    public bool IsActive { get; set; }

    public ClientListItemDto() { }

    public ClientListItemDto(Client entity, OwnerSummaryDto? summary)
    {
        Id = entity.Id;
        FullName = entity.FullName;
        Cpf = entity.Cpf?.Value;
        BirthDate = entity.BirthDate;
        MainPhone = summary?.MainPhone;
        CityState = summary?.CityState;
        IsActive = entity.IsActive;
    }
}
