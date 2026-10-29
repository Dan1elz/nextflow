namespace Nextflow.Domain.Interfaces.Domain.Models;

public interface IDeletable
{
    bool IsActive { get; }
    void Delete();
    void Reactivate();
}
