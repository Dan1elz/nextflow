using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Contacts;

public class ContactRequestDto : UpdateContactRequestDto
{
    public OwnerTypeEnum OwnerType { get; set; }
    public Guid OwnerId { get; set; }
}
