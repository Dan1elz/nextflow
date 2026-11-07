using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Clients;

/// <summary>
/// Linha da listagem (T-CD3). <see cref="OrderCount"/>, <see cref="TotalSpent"/> e <see cref="LastPurchaseOn"/> vêm das
/// vendas pagas do cliente; zero ou nulos sem venda.
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
