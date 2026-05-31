using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Models;
using System.ComponentModel.DataAnnotations;

namespace Nextflow.Domain.Dtos;

public class CreateCountryDto : BaseDto
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O Nome do país deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Nome do país é obrigatório.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2, MinimumLength = 2, ErrorMessage = "O acrônimo do país deve ter no máximo 2 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O acrônimo do país é obrigatório.")]
    public string AcronymIso { get; set; } = string.Empty;

    private string? _bacenCode;
    [StringLength(255, MinimumLength = 2, ErrorMessage = "O código do Bacen deve ter no máximo 255 caracteres e no mínimo 2 caracteres.")]
    public string? BacenCode
    {
        get => _bacenCode;
        set => _bacenCode = string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public class UpdateCountryDto : CreateCountryDto { }

public class CountryResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AcronymIso { get; set; } = string.Empty;
    public string? BacenCode { get; set; } = string.Empty;

    public CountryResponseDto() { }

    public CountryResponseDto(Country entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        AcronymIso = entity.AcronymIso;
        BacenCode = entity.BacenCode;
    }
}