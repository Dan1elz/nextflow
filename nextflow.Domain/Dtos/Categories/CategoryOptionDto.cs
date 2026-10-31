using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Categories;

/// <summary>Combo de categoria: <see cref="Path"/> é "Pai &gt; Filha" na subcategoria.</summary>
public class CategoryOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string Path { get; set; } = string.Empty;

    public CategoryOptionDto() { }

    public CategoryOptionDto(Category entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        ParentId = entity.ParentId;
        Path = entity.Parent is null ? entity.Name : $"{entity.Parent.Name} > {entity.Name}";
    }
}
