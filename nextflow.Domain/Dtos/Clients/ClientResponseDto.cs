using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Clients;

/// <summary>Detalhe (T-CD4). Os totais de compra dependem de pedidos: ficam zero ou nulos até a fase 2.</summary>
public class ClientResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Cpf { get; set; }
    public DateOnly? BirthDate { get; set; }
    public bool IsActive { get; set; }
    public AddressResponseDto? MainAddress { get; set; }
    public ContactResponseDto? MainContact { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal AverageTicket { get; set; }
    public DateOnly? LastPurchaseOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ClientResponseDto() { }

    public ClientResponseDto(Client entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        LastName = entity.LastName;
        Cpf = entity.Cpf?.Value;
        BirthDate = entity.BirthDate;
        IsActive = entity.IsActive;
        CreatedAt = entity.CreatedAt;
        UpdatedAt = entity.UpdatedAt;
    }
}
