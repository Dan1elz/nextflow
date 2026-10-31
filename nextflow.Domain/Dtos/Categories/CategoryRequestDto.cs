namespace Nextflow.Domain.Dtos.Categories;

public class CategoryRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Nulo = nível 1. Só categoria de nível 1 pode ser pai (RN-CE23).</summary>
    public Guid? ParentId { get; set; }
}
