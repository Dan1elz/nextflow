using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class StoreRepository(AppDbContext context) : BaseRepository<Store>(context), IStoreRepository
{
    public Task<List<Guid>> GetActiveIds(CancellationToken ct = default)
        => _context.Set<Store>().AsNoTracking().Where(s => s.IsActive).Select(s => s.Id).ToListAsync(ct);
}
