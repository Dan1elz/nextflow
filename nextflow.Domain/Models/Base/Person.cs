using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Dtos.Base;

namespace Nextflow.Domain.Models.Base;

using Nextflow.Domain.Interfaces.Models;

public abstract class Person : BaseModel, IDeletable
{
    [StringLength(25, MinimumLength = 2, ErrorMessage = "O Nome deve ter no máximo 25 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Nome é obrigatório.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(25, MinimumLength = 2, ErrorMessage = "O Sobrenome deve ter no máximo 25 caracteres e no mínimo 2 caracteres."), Required(ErrorMessage = "O Sobrenome é obrigatório.")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(14, ErrorMessage = "O CPF deve ter no máximo 14 caracteres."), Required(ErrorMessage = "O CPF é obrigatório.")]
    public string CPF { get; set; } = string.Empty;

    [Required(ErrorMessage = "A Data de Nascimento é obrigatória.")]
    public DateOnly BirthDate { get; set; }

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

    protected Person() : base() { }

    protected Person(CreatePersonDto dto) : base()
    {
        Name = dto.Name;
        LastName = dto.LastName;
        CPF = dto.CPF;
        BirthDate = dto.BirthDate;
    }

    public void Update(UpdatePersonDto dto)
    {
        Name = dto.Name;
        LastName = dto.LastName;
        CPF = dto.CPF;
        BirthDate = dto.BirthDate;
        Update();
    }
}
