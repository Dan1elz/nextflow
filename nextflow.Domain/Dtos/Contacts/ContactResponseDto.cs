using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Contacts;

public class ContactResponseDto
{
    public Guid Id { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsMain { get; set; }

    public ContactResponseDto() { }

    public ContactResponseDto(Contact entity)
    {
        Id = entity.Id;
        OwnerType = entity.OwnerType.GetApiName();
        OwnerId = entity.OwnerId;
        Type = entity.Type.GetApiName();
        Value = entity.Value;
        IsMain = entity.IsMain;
    }
}
