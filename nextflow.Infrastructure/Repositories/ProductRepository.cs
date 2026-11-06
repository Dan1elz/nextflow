using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ProductRepository(AppDbContext context) : BaseRepository<Product>(context), IProductRepository
{
    public async Task<Dictionary<AbcClassEnum, int>> CountByAbcClass(CancellationToken ct = default)
        => await _context.Set<Product>().AsNoTracking()
            .Where(p => p.IsActive && p.AbcClass != null)
            .GroupBy(p => p.AbcClass!.Value)
            .Select(g => new { AbcClass = g.Key, Count = g.Count() })
            .ToDictionaryAsync(row => row.AbcClass, row => row.Count, ct);
}
