using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public void Rollback()
        => context.ChangeTracker.Clear();
}
