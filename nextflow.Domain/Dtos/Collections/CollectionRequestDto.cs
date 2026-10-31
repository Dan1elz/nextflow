namespace Nextflow.Domain.Dtos.Collections;

public class CollectionRequestDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly? StartsOn { get; set; }
    public DateOnly? EndsOn { get; set; }
}
