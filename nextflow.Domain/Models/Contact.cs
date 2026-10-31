using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Models;

/// <summary>Contato de cliente ou fornecedor (RN-CD05). Exclusão física (decisão 4 de 29/10), por isso não é IDeletable.</summary>
public class Contact : BaseModel, IOwned, IUpdatable<UpdateContactRequestDto>
{
    public OwnerTypeEnum OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public ContactTypeEnum Type { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public bool IsMain { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public bool IsPhone => Type is ContactTypeEnum.Phone or ContactTypeEnum.WhatsApp;

    public override string Preposition => "o";
    public override string Singular => "contato";
    public override string Plural => "contatos";

    private Contact() { }

    public Contact(ContactRequestDto dto)
    {
        OwnerType = dto.OwnerType;
        OwnerId = dto.OwnerId;
        Apply(dto);
    }

    public void Update(UpdateContactRequestDto dto)
    {
        Apply(dto);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMain(bool isMain)
    {
        if (IsMain == isMain)
            return;

        IsMain = isMain;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(UpdateContactRequestDto dto)
    {
        Type = dto.Type;
        Value = dto.Type == ContactTypeEnum.Email ? dto.Value.Trim().ToLowerInvariant() : Digits.Only(dto.Value);
        IsMain = dto.IsMain;
    }
}
