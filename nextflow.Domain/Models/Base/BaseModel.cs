using System.ComponentModel.DataAnnotations;
using System.Net.NetworkInformation;
using Nextflow.Domain.Interfaces.Models;

namespace Nextflow.Domain.Models.Base;

public class BaseModel : IEntityMetadata
{
    [Key]
    public Guid Id { get; private set; }
    public DateTime CreateAt { get; private set; }

    public virtual string Preposition => throw new NotImplementedException();
    public virtual string Singular => throw new NotImplementedException();
    public virtual string Plural => throw new NotImplementedException();

    protected BaseModel()
    {
        Id = Guid.NewGuid();
        CreateAt = DateTime.UtcNow;
    }
}
