using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

public abstract class BaseModel : IEntityMetadata
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public abstract string Preposition { get; }
    public abstract string Singular { get; }
    public abstract string Plural { get; }
}
