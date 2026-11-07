using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class OrderRepository(AppDbContext context) : BaseRepository<Order>(context), IOrderRepository
{
    public async Task<Dictionary<Guid, PurchaseStatsDto>> GetClientStats(IReadOnlyCollection<Guid> clientIds, CancellationToken ct = default)
    {
        if (clientIds.Count == 0)
            return [];

        var rows = await _context.Set<Order>().AsNoTracking()
            .Where(o => o.Status == OrderStatusEnum.Paid && o.ClientId != null && clientIds.Contains(o.ClientId.Value))
            .GroupBy(o => o.ClientId!.Value)
            .Select(g => new { ClientId = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total), LastAt = g.Max(o => o.PaidAt) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.ClientId, r => new PurchaseStatsDto(r.Count, r.Total, r.LastAt));
    }

    public Task<List<SaleLineDto>> GetPaidSaleLines(CancellationToken ct = default)
        => _context.Set<OrderItem>().AsNoTracking()
            .Where(i => i.Order.Status == OrderStatusEnum.Paid && i.Order.PaidAt != null)
            .Select(i => new SaleLineDto(i.Variant.ProductId, i.Order.PaidAt!.Value, i.Quantity, i.PromotionId != null, i.LineTotal))
            .ToListAsync(ct);

    public async Task<Dictionary<Guid, decimal>> GetPaidQuantityByVariant(DateTime sinceUtc, CancellationToken ct = default)
        => await _context.Set<OrderItem>().AsNoTracking()
            .Where(i => i.Order.Status == OrderStatusEnum.Paid && i.Order.PaidAt != null && i.Order.PaidAt >= sinceUtc)
            .GroupBy(i => i.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(row => row.VariantId, row => row.Quantity, ct);

    public Task<List<Guid>> GetClientIdsWithPurchase(DateTime? since, CancellationToken ct = default)
        => _context.Set<Order>().AsNoTracking()
            .Where(o => o.Status == OrderStatusEnum.Paid && o.ClientId != null && (since == null || o.PaidAt >= since))
            .Select(o => o.ClientId!.Value)
            .Distinct()
            .ToListAsync(ct);
}
