using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stores;

public class StoreResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? DefaultSaleLocationId { get; set; }
    public Guid? DefaultReceiveLocationId { get; set; }
    public List<LocationResponseDto> Locations { get; set; } = [];

    public StoreResponseDto() { }

    public StoreResponseDto(Store entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        IsActive = entity.IsActive;
        DefaultSaleLocationId = entity.DefaultSaleLocationId;
        DefaultReceiveLocationId = entity.DefaultReceiveLocationId;
        Locations = [.. entity.Locations.OrderBy(l => l.Name).Select(l => new LocationResponseDto(l))];
    }
}
