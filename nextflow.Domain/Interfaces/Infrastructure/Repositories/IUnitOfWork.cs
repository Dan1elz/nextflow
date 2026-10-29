namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Único ponto de commit: um <see cref="CommitAsync"/> por requisição (ADR 0010).</summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
    void Rollback();
}
