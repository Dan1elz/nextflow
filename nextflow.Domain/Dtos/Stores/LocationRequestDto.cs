namespace Nextflow.Domain.Dtos.Stores;

public class LocationRequestDto : UpdateLocationRequestDto
{
    public Guid StoreId { get; set; }
}
