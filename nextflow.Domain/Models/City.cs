using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Dtos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nextflow.Domain.Models;

[Table("cities")]
public class City : BaseModel, IUpdatable<UpdateCityDto>, IDeletable
{
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O Nome da cidade deve ter no máximo 100 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Nome da cidade é obrigatório.")]
    public string Name { get; private set; } = string.Empty;

    [StringLength(7, MinimumLength = 7, ErrorMessage = "O código IBGE deve ter no máximo 7 caracteres e no mínimo 7 caracteres."), Required(ErrorMessage = "O código IBGE é obrigatório.")]
    public string IbgeCode { get; private set; } = string.Empty;

    [ForeignKey("states"), Required(ErrorMessage = "Id do estado é obrigatório.")]
    public Guid StateId { get; private set; }
    public virtual State? State { get; set; }
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

    public override string Preposition => "a";
    public override string Singular => "cidade";
    public override string Plural => "cidades";

    private City() : base() { }

    public City(CreateCityDto dto) : base()
    {
        Name = dto.Name;
        IbgeCode = dto.IbgeCode;
        StateId = dto.StateId;
    }

    public void Update(UpdateCityDto dto)
    {
        Name = dto.Name;
        IbgeCode = dto.IbgeCode;
        StateId = dto.StateId;
        Update();
    }
}

