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
        Path = BuildPath(entity.Parent?.Name, entity.Name);
    }

    public CategoryOptionDto(CategorySummaryDto summary)
    {
        Id = summary.Id;
        Name = summary.Name;
        ParentId = summary.ParentId;
        Path = BuildPath(summary.ParentName, summary.Name);
    }

    private static string BuildPath(string? parentName, string name)
        => parentName is null ? name : $"{parentName} > {name}";
}
