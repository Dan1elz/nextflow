namespace Nextflow.Domain.Dtos.Categories;

/// <summary>Categoria com o nome da mãe, lida por consulta própria do repositório para compor o combo do formulário.</summary>
public record CategorySummaryDto(Guid Id, string Name, Guid? ParentId, string? ParentName);
