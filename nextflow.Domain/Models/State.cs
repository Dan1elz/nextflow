using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Models;

[Table("states")]
public class State : BaseModel, IUpdatable<UpdateStateDto>, IDeletable
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O Nome do estado deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Nome do estado é obrigatório.")]
    public string Name { get; private set; } = string.Empty;

    [StringLength(2, MinimumLength = 2, ErrorMessage = "O acrônimo do estado deve ter no máximo 2 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O acrônimo do estado é obrigatório.")]
    public string Acronym { get; private set; } = string.Empty;

    [StringLength(2, MinimumLength = 2, ErrorMessage = "O código IBGE deve ter no máximo 2 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O código IBGE é obrigatório.")]
    public string IbgeCode { get; private set; } = string.Empty;

    [ForeignKey("countries"), Required(ErrorMessage = "O País é obrigatório.")]
    public Guid CountryId { get; private set; }
    public virtual Country? Country { get; private set; }

    public virtual ICollection<City> Cities { get; set; } = [];
    public virtual ICollection<Address> Addresses { get; set; } = [];

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
    public override string Singular => "estado";
    public override string Plural => "estados";

    private State() : base() { }

    public State(CreateStateDto dto) : base()
    {
        Name = dto.Name;
        Acronym = dto.Acronym;
        IbgeCode = dto.IbgeCode;
        CountryId = dto.CountryId;
    }

    public void Update(UpdateStateDto dto)
    {
        Name = dto.Name;
        Acronym = dto.Acronym;
        IbgeCode = dto.IbgeCode;
        CountryId = dto.CountryId;
        Update();
    }
}



