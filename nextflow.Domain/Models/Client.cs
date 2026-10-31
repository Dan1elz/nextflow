using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Domain.Models;

public class Client : BaseModel, IDeletable, IUpdatable<ClientRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public string? LastName { get; private set; }
    public TaxDocument? Cpf { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public string FullName => $"{Name} {LastName}".Trim();

    public override string Preposition => "o";
    public override string Singular => "cliente";
    public override string Plural => "clientes";

    private Client() { }

    public Client(ClientRequestDto dto) => Apply(dto);

    /// <summary>Cadastro rápido na venda (T-CD4): só o nome; o telefone vira contato no mesmo commit.</summary>
    public Client(QuickCreateClientRequestDto dto) => Name = dto.Name.Trim();

    public void Update(ClientRequestDto dto)
    {
        Apply(dto);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(ClientRequestDto dto)
    {
        Name = dto.Name.Trim();
        LastName = string.IsNullOrWhiteSpace(dto.LastName) ? null : dto.LastName.Trim();
        Cpf = string.IsNullOrWhiteSpace(dto.Cpf) ? null : TaxDocument.ParseCpf(dto.Cpf);
        BirthDate = dto.BirthDate;
    }
}
