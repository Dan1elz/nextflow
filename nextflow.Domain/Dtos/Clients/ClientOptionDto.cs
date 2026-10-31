using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Clients;

/// <summary>Combo de cliente na venda (T-CD4): mostra o CPF mascarado e traz o telefone.</summary>
public class ClientOptionDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Phone { get; set; }

    public ClientOptionDto() { }

    public ClientOptionDto(Client entity, string? phone)
    {
        Id = entity.Id;
        Label = entity.Cpf != null ? $"{entity.FullName} · {entity.Cpf.MaskedPartially}" : entity.FullName;
        Phone = phone;
    }
}
