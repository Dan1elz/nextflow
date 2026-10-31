namespace Nextflow.Domain.Dtos.Clients;

/// <summary>Cliente (T-CD4). CPF opcional, único e válido quando informado (RN-CD01, L-CP6).</summary>
public class ClientRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Cpf { get; set; }
    public DateOnly? BirthDate { get; set; }
}
