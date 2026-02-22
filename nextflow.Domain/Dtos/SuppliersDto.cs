using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Models;
using System.ComponentModel.DataAnnotations;

namespace Nextflow.Domain.Dtos;

public class CreateSupplierDto : BaseDto
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O Nome do forncedor deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Nome do forncedor é obrigatório.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "O CNPJ do forncedor é obrigatório.")]
    public string CNPJ { get; set; } = string.Empty;
}

public class UpdateSupplierDto : CreateSupplierDto { }

public class SupplierResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CNPJ { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    public SupplierResponseDto() { }

    public SupplierResponseDto(Supplier entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        CNPJ = entity.CNPJ;
        IsActive = entity.IsActive;
    }
}
