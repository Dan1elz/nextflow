using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            // O dispose desfaz o banco; o rastreador também precisa esquecer o que ficou como Added.
            Rollback();
            throw;
        }
    }

    public void Rollback()
        => context.ChangeTracker.Clear();
}
