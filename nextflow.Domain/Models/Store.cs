using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>
/// Loja: onde se vende (ADR 0009). Os padrões de venda e recebimento apontam para <see cref="StockLocation"/>, e o local
/// aponta para a loja (DOMAIN_LEVELS, par 1): sem navegação de volta, para o EF não enxergar o ciclo ao gravar os dois
/// no mesmo commit. A FK fica no banco, adiada para o fim da transação (migration AddStores).
/// </summary>
public class Store : BaseModel, IDeletable, IUpdatable<UpdateStoreRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public Guid? DefaultSaleLocationId { get; private set; }
    public Guid? DefaultReceiveLocationId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual ICollection<StockLocation> Locations { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "loja";
    public override string Plural => "lojas";

    private Store() { }

    public Store(StoreRequestDto dto) => Name = dto.Name.Trim();

    public void Update(UpdateStoreRequestDto dto)
    {
        Name = dto.Name.Trim();
        SetDefaults(dto.DefaultSaleLocationId, dto.DefaultReceiveLocationId);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDefaults(Guid saleLocationId, Guid receiveLocationId)
    {
        DefaultSaleLocationId = saleLocationId;
        DefaultReceiveLocationId = receiveLocationId;
    }

    public bool IsDefaultLocation(Guid locationId)
        => DefaultSaleLocationId == locationId || DefaultReceiveLocationId == locationId;

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
