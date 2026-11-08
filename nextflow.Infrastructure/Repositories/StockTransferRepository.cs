using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class StockTransferRepository(AppDbContext context) : IStockTransferRepository
{
    public async Task AddAsync(StockTransfer transfer, CancellationToken ct = default)
        => await context.Set<StockTransfer>().AddAsync(transfer, ct);

    public Task<StockTransfer?> GetDetail(Guid id, CancellationToken ct = default)
        => context.Set<StockTransfer>().AsNoTracking()
            .Include(t => t.FromLocation).ThenInclude(l => l.Store)
            .Include(t => t.ToLocation).ThenInclude(l => l.Store)
            .Include(t => t.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
}
