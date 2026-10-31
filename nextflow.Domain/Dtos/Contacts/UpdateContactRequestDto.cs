using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Contacts;

/// <summary>Campos que a edição aceita: o dono não muda.</summary>
public class UpdateContactRequestDto
{
    public ContactTypeEnum Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public bool IsMain { get; set; }
}
