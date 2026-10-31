using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Models;

/// <summary>Endereço de cliente ou fornecedor (RN-CD05). Exclusão física (decisão 4 de 29/10), por isso não é IDeletable.</summary>
public class Address : BaseModel, IOwned, IUpdatable<UpdateAddressRequestDto>
{
    public OwnerTypeEnum OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public string ZipCode { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string Neighborhood { get; private set; } = string.Empty;
    public Guid CityId { get; private set; }
    public bool IsMain { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public virtual City City { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "endereço";
    public override string Plural => "endereços";

    private Address() { }

    public Address(AddressRequestDto dto)
    {
        OwnerType = dto.OwnerType;
        OwnerId = dto.OwnerId;
        Apply(dto);
    }

    public void Update(UpdateAddressRequestDto dto)
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

    private void Apply(UpdateAddressRequestDto dto)
    {
        ZipCode = Digits.Only(dto.ZipCode);
        Street = dto.Street.Trim();
        Number = dto.Number.Trim();
        Complement = string.IsNullOrWhiteSpace(dto.Complement) ? null : dto.Complement.Trim();
        Neighborhood = dto.Neighborhood.Trim();
        CityId = dto.CityId;
        IsMain = dto.IsMain;
    }
}
