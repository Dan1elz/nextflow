using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Collections;

/// <summary>Coleção (T5). <see cref="ProductCount"/> fica zero até a F1.3, que traz <c>products.collection_id</c>.</summary>
public class CollectionResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
    public int ProductCount { get; set; }
    public bool IsActive { get; set; }

    public CollectionResponseDto() { }

    public CollectionResponseDto(Collection entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        StartsOn = entity.StartsOn;
        EndsOn = entity.EndsOn;
        IsActive = entity.IsActive;
    }
}
