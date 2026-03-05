using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;
namespace Nextflow.Domain.Models;

[Table("categories")]
public class Category : BaseModel, IUpdatable<UpdateCategoryDto>, IDeletable
{
    [StringLength(100, ErrorMessage = "A DescriÃ§Ã£o deve ter no maximo 100 caracteres")]
    [Required(ErrorMessage = "A DescriÃ§Ã£o Ã© obrigatÃ³ria")]
    public string Description { get; private set; } = string.Empty;
    public virtual ICollection<CategoryProduct> CategoryProducts { get; set; } = [];

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
    public override string Singular => "categoria";
    public override string Plural => "categorias";

    private Category() : base() { }

    public Category(CreateCategoryDto dto) : base()
    {
        Description = dto.Description;
    }

    public void Update(UpdateCategoryDto dto)
    {
        Description = dto.Description;
        Update();
    }
}

