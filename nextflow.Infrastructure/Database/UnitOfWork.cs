using Nextflow.Domain.Interfaces.Repositories;

namespace Nextflow.Infrastructure.Database;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private readonly AppDbContext _context = context;

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (_context.Database.CurrentTransaction != null)
            return await action(ct);

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await action(ct);
            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // Entidades rastreadas podem refletir gravações desfeitas pelo rollback
            _context.ChangeTracker.Clear();
            throw;
        }
    }
}
