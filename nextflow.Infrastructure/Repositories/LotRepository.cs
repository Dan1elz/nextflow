using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class LotRepository(AppDbContext context) : ILotRepository
{
    public async Task AddAsync(Lot lot, CancellationToken ct = default)
        => await context.Set<Lot>().AddAsync(lot, ct);

    public async Task<List<Lot>> GetForVariants(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
    {
        var stored = await context.Set<Lot>().AsNoTracking().Where(l => variantIds.Contains(l.VariantId)).ToListAsync(ct);
        var pending = context.ChangeTracker.Entries<Lot>()
            .Where(e => e.State == EntityState.Added && variantIds.Contains(e.Entity.VariantId))
            .Select(e => e.Entity);
        return [.. stored.Where(s => pending.All(p => p.Id != s.Id)), .. pending];
    }
}
