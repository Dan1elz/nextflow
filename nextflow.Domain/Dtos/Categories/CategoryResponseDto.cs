using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Categories;

public class CategoryResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }

    public CategoryResponseDto() { }

    public CategoryResponseDto(Category entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Description = entity.Description;
        ParentId = entity.ParentId;
        IsActive = entity.IsActive;
    }
}
