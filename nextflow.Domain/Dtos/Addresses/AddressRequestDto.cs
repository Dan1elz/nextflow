using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Addresses;

public class AddressRequestDto : UpdateAddressRequestDto
{
    public OwnerTypeEnum OwnerType { get; set; }
    public Guid OwnerId { get; set; }
}
