namespace Nextflow.Domain.Interfaces.Domain.Models;

public interface IUpdatable<in TRequestDto>
{
    void Update(TRequestDto dto);
}
