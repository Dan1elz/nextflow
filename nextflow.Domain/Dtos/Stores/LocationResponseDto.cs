using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stores;

public class LocationResponseDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsSellable { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Algum saldo diferente de zero no local (RN-CE35): a tela avisa que não dá para inativar. Lê os saldos carregados.</summary>
    public bool HasStock { get; set; }

    public LocationResponseDto() { }

    public LocationResponseDto(StockLocation entity)
    {
        Id = entity.Id;
        StoreId = entity.StoreId;
        Name = entity.Name;
        IsSellable = entity.IsSellable;
        IsActive = entity.IsActive;
        HasStock = entity.Balances.Any(b => b.Quantity != 0);
    }
}
