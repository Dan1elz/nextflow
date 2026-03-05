using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Dtos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nextflow.Domain.Models;

[Table("addresses")]
public class Address : BaseModel, IUpdatable<UpdateAddressDto>, IDeletable
{
    [ForeignKey("clients")]
    public Guid? ClientId { get; private set; }
    public virtual Client? Client { get; set; }

    [ForeignKey("suppliers")]
    public Guid? SupplierId { get; private set; }
    public virtual Supplier? Supplier { get; set; }

    [StringLength(100, MinimumLength = 2, ErrorMessage = "A Descrição do endereço deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "A descrição do endereço é obrigatório.")]
    public string Description { get; private set; } = string.Empty;

    [StringLength(100, MinimumLength = 2, ErrorMessage = "O nome da rua deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O nome da rua é obrigatório.")]
    public string Street { get; private set; } = string.Empty;

    [StringLength(10, MinimumLength = 1, ErrorMessage = "O número deve ter no máximo 10 caracteres e no mínimo 1 caracteres."), Required(ErrorMessage = "O número é obrigatório.")]
    public string Number { get; private set; } = string.Empty;

    [StringLength(100, MinimumLength = 1, ErrorMessage = "O distrito deve ter no máximo 100 caracteres e no mínimo 1 caracteres."), Required(ErrorMessage = "O distrito é obrigatório.")]
    public string District { get; private set; } = string.Empty;

    [ForeignKey("cities"), Required(ErrorMessage = "Id da cidade é obrigatório.")]
    public Guid CityId { get; private set; }
    public virtual City? City { get; set; }

    [ForeignKey("States"), Required(ErrorMessage = "Id do estado é obrigatório.")]
    public Guid StateId { get; private set; }
    public virtual State? State { get; set; }

    [StringLength(100, MinimumLength = 0, ErrorMessage = "O complemento deve ter no máximo 100 cqracteres.")]
    public string Complement { get; private set; } = string.Empty;

    [StringLength(9, MinimumLength = 9, ErrorMessage = "O CEP deve ter 9 caracteres."), Required(ErrorMessage = "O CEP é obrigatório.")]
    public string ZipCode { get; private set; } = string.Empty;

    public DateTime? UpdateAt { get; private set; }
    public bool IsActive { get; set; } = true;

    public void Update()
    {
        UpdateAt = DateTime.UtcNow;
    }
    public void Delete()
    {
        IsActive = false;
        UpdateAt = DateTime.UtcNow;
    }
    public void Reactivate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateAt = DateTime.UtcNow;
        }
    }

    public override string Preposition => "o";
    public override string Singular => "endereço";
    public override string Plural => "endereços";

    private Address() : base() { }

    public Address(CreateAddressDto dto) : base()
    {
        ClientId = dto.ClientId;
        SupplierId = dto.SupplierId;
        Description = dto.Description;
        Street = dto.Street;
        Number = dto.Number;
        District = dto.District;
        CityId = dto.CityId;
        StateId = dto.StateId;
        Complement = dto.Complement;
        ZipCode = dto.ZipCode;
    }

    public void Update(UpdateAddressDto dto)
    {
        Description = dto.Description;
        Street = dto.Street;
        Number = dto.Number;
        District = dto.District;
        CityId = dto.CityId;
        StateId = dto.StateId;
        Complement = dto.Complement;
        ZipCode = dto.ZipCode;
    }
}

