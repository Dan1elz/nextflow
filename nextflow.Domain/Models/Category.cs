using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>Categoria de produto, com autorreferência limitada a 2 níveis no use case (RN-CE23).</summary>
public class Category : BaseModel, IDeletable, IUpdatable<CategoryRequestDto>
{
    public Guid? ParentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual Category? Parent { get; private set; }
    public virtual ICollection<Category> Children { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "categoria";
    public override string Plural => "categorias";

    private Category() { }

    public Category(CategoryRequestDto dto) => Apply(dto);

    public void Update(CategoryRequestDto dto)
    {
        Apply(dto);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private void Apply(CategoryRequestDto dto)
    {
        ParentId = dto.ParentId;
        Name = dto.Name.Trim();
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
    }
}
