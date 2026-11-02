using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>Local de estoque: onde a mercadoria fica (ADR 0009).</summary>
public class StockLocation : BaseModel, IDeletable, IUpdatable<UpdateLocationRequestDto>
{
    /// <summary>Nome do local que toda loja nova ganha.</summary>
    public const string DefaultName = "Loja";

    public Guid StoreId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsSellable { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual Store Store { get; private set; } = null!;
    public virtual ICollection<StockBalance> Balances { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "local";
    public override string Plural => "locais";

    private StockLocation() { }

    public StockLocation(LocationRequestDto dto)
    {
        StoreId = dto.StoreId;
        Name = dto.Name.Trim();
        IsSellable = dto.IsSellable;
    }

    public void Update(UpdateLocationRequestDto dto)
    {
        Name = dto.Name.Trim();
        IsSellable = dto.IsSellable;
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
}
