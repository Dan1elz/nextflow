using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Domain.Models;

public class Supplier : BaseModel, IDeletable, IUpdatable<SupplierRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public TaxDocument Cnpj { get; private set; } = null!;
    public int LeadTimeDays { get; private set; } = 7;
    public string? Note { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "fornecedor";
    public override string Plural => "fornecedores";

    private Supplier() { }

    public Supplier(SupplierRequestDto dto) => Apply(dto);

    public void Update(SupplierRequestDto dto)
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

    private void Apply(SupplierRequestDto dto)
    {
        Name = dto.Name.Trim();
        Cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        LeadTimeDays = dto.LeadTimeDays;
        Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
    }
}
