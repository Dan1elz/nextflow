using Nextflow.Domain.Dtos.Cities;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Addresses;

public class AddressResponseDto
{
    public Guid Id { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string ZipCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string StateAbbreviation { get; set; } = string.Empty;
    public bool IsMain { get; set; }

    public AddressResponseDto() { }

    /// <summary>Com a navegação <c>City.State</c> carregada pelo <c>GetInclude()</c> da listagem.</summary>
    public AddressResponseDto(Address entity) : this(entity, null)
    {
        if (entity.City != null)
        {
            CityName = entity.City.Name;
            StateAbbreviation = entity.City.State != null ? entity.City.State.Acronym : string.Empty;
        }
    }

    /// <summary>Com a cidade lida por projeção, quando a navegação não está carregada ou mudou neste commit.</summary>
    public AddressResponseDto(Address entity, CitySummaryDto? city)
    {
        Id = entity.Id;
        OwnerType = entity.OwnerType.GetApiName();
        OwnerId = entity.OwnerId;
        ZipCode = entity.ZipCode;
        Street = entity.Street;
        Number = entity.Number;
        Complement = entity.Complement;
        Neighborhood = entity.Neighborhood;
        CityId = entity.CityId;
        IsMain = entity.IsMain;

        if (city != null)
        {
            CityName = city.Name;
            StateAbbreviation = city.StateAcronym;
        }
    }
}
