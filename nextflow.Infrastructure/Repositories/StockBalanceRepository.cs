using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class StockBalanceRepository(AppDbContext context) : BaseRepository<StockBalance>(context), IStockBalanceRepository
{
    public async Task<Dictionary<Guid, decimal>> SumByVariant(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
        => await _context.Set<StockBalance>().AsNoTracking()
            .Where(b => variantIds.Contains(b.VariantId))
            .GroupBy(b => b.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(b => b.Quantity) })
            .ToDictionaryAsync(row => row.VariantId, row => row.Quantity, ct);

    public async Task<Dictionary<Guid, decimal>> SumByProduct(IReadOnlyCollection<Guid> productIds, Guid? storeId, CancellationToken ct = default)
        => await _context.Set<StockBalance>().AsNoTracking()
            .Where(b => productIds.Contains(b.Variant.ProductId)
                && b.Location.IsActive
                && b.Location.Store.IsActive
                && (storeId == null || b.Location.StoreId == storeId))
            .GroupBy(b => b.Variant.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(b => b.Quantity) })
            .ToDictionaryAsync(row => row.ProductId, row => row.Quantity, ct);
}
