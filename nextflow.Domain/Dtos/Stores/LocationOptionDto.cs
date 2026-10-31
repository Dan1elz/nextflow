using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stores;

/// <summary>Combo de local (T7): as telas de estoque, venda e compra precisam saber a loja e se vende.</summary>
public class LocationOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public bool IsSellable { get; set; }

    public LocationOptionDto() { }

    public LocationOptionDto(StockLocation entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        StoreId = entity.StoreId;
        StoreName = entity.Store != null ? entity.Store.Name : string.Empty;
        IsSellable = entity.IsSellable;
    }
}
