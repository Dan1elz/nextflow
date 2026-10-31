namespace Nextflow.Domain.Dtos.Addresses;

/// <summary>Campos que a edição aceita: o dono não muda.</summary>
public class UpdateAddressRequestDto
{
    public string ZipCode { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string? Complement { get; set; }
    public string Neighborhood { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public bool IsMain { get; set; }
}
