using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Categories;

/// <summary>Nó da árvore (T4). <see cref="ProductCount"/> conta os produtos ativos vinculados direto à categoria (RN-CE53).</summary>
public class CategoryNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public List<CategoryNodeDto> Children { get; set; } = [];

    public CategoryNodeDto() { }

    public CategoryNodeDto(Category entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Description = entity.Description;
        ParentId = entity.ParentId;
        IsActive = entity.IsActive;
        ProductCount = entity.Products.Count;
        Children = [.. entity.Children.Select(child => new CategoryNodeDto(child))];
    }
}
