using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class PurchaseItemRepository(AppDbContext context) : BaseRepository<PurchaseItem>(context), IPurchaseItemRepository
{
    public async Task<Dictionary<Guid, decimal>> SumIncomingByVariant(IReadOnlyCollection<Guid> variantIds, Guid storeId, CancellationToken ct = default)
        => await _context.Set<PurchaseItem>().AsNoTracking()
            .Where(i => variantIds.Contains(i.VariantId)
                && i.PurchaseOrder.StoreId == storeId
                && (i.PurchaseOrder.Status == PurchaseStatusEnum.Budget || i.PurchaseOrder.Status == PurchaseStatusEnum.Pending))
            .GroupBy(i => i.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(row => row.VariantId, row => row.Quantity, ct);

    public async Task<Dictionary<Guid, decimal>> SumIncomingByProduct(IReadOnlyCollection<Guid> productIds, Guid storeId, CancellationToken ct = default)
        => await _context.Set<PurchaseItem>().AsNoTracking()
            .Where(i => productIds.Contains(i.Variant.ProductId)
                && i.PurchaseOrder.StoreId == storeId
                && (i.PurchaseOrder.Status == PurchaseStatusEnum.Budget || i.PurchaseOrder.Status == PurchaseStatusEnum.Pending))
            .GroupBy(i => i.Variant.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(row => row.ProductId, row => row.Quantity, ct);
}
