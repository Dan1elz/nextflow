using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Cliente do pedido; o CPF sai mascarado.</summary>
public class OrderClientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Cpf { get; set; }

    public OrderClientDto() { }

    public OrderClientDto(Client entity)
    {
        Id = entity.Id;
        Name = entity.FullName;
        Cpf = entity.Cpf?.Masked;
    }
}
