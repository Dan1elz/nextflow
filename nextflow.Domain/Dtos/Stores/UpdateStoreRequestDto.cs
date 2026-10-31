namespace Nextflow.Domain.Dtos.Stores;

public class UpdateStoreRequestDto
{
    public string Name { get; set; } = string.Empty;
    public Guid DefaultSaleLocationId { get; set; }
    public Guid DefaultReceiveLocationId { get; set; }
}
