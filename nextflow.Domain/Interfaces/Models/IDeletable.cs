namespace Nextflow.Domain.Interfaces.Models;

public interface IDeletable
{
    bool IsActive { get; set; }
    void Delete();
    void Reactivate();
}
