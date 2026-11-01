using Nextflow.Domain.Dtos.Collections;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>Coleção ou temporada, opcional no produto (RN-CE20, RN-CE40).</summary>
public class Collection : BaseModel, IDeletable, IUpdatable<CollectionRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public DateOnly? StartsOn { get; private set; }
    public DateOnly? EndsOn { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual ICollection<Product> Products { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "coleção";
    public override string Plural => "coleções";

    private Collection() { }

    public Collection(CollectionRequestDto dto) => Apply(dto);

    public void Update(CollectionRequestDto dto)
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

    private void Apply(CollectionRequestDto dto)
    {
        Name = dto.Name.Trim();
        StartsOn = dto.StartsOn;
        EndsOn = dto.EndsOn;
    }
}
