using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class PurchaseOrderRepository(AppDbContext context) : BaseRepository<PurchaseOrder>(context), IPurchaseOrderRepository
{
    private IQueryable<PurchaseOrder> Sent()
        => _context.Set<PurchaseOrder>().AsNoTracking()
            .Where(p => (p.Status == PurchaseStatusEnum.Pending || p.Status == PurchaseStatusEnum.Received) && p.SentAt != null);

    public async Task<Dictionary<Guid, PurchaseStatsDto>> GetSupplierStats(IReadOnlyCollection<Guid> supplierIds, CancellationToken ct = default)
    {
        if (supplierIds.Count == 0)
            return [];

        var rows = await Sent()
            .Where(p => supplierIds.Contains(p.SupplierId))
            .GroupBy(p => p.SupplierId)
            .Select(g => new
            {
                SupplierId = g.Key,
                Count = g.Count(),
                Total = g.Sum(p => p.Total),
                LastAt = g.Max(p => p.SentAt),
                AverageLead = g.Average(p => (double?)p.RealLeadTimeDays),
            })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.SupplierId, r => new PurchaseStatsDto(r.Count, r.Total, r.LastAt, r.AverageLead));
    }

    public Task<List<Guid>> GetSupplierIdsWithPurchase(DateTime? since, CancellationToken ct = default)
        => Sent().Where(p => since == null || p.SentAt >= since).Select(p => p.SupplierId).Distinct().ToListAsync(ct);

    public async Task<List<ReceivedLeadTimeDto>> GetReceivedLeadTimes(DateTime sinceUtc, CancellationToken ct = default)
    {
        var rows = await _context.Set<PurchaseOrder>().AsNoTracking()
            .Where(p => p.Status == PurchaseStatusEnum.Received && p.RealLeadTimeDays != null && p.ReceivedAt != null && p.ReceivedAt >= sinceUtc)
            .Select(p => new { p.SupplierId, Days = p.RealLeadTimeDays!.Value, ReceivedAt = p.ReceivedAt!.Value })
            .ToListAsync(ct);
        return rows.Select(r => new ReceivedLeadTimeDto(r.SupplierId, r.Days, r.ReceivedAt)).ToList();
    }

    public async Task<Dictionary<Guid, decimal>> GetIncomingByVariant(CancellationToken ct = default)
        => await _context.Set<PurchaseItem>().AsNoTracking()
            .Where(i => i.PurchaseOrder.Status == PurchaseStatusEnum.Budget || i.PurchaseOrder.Status == PurchaseStatusEnum.Pending)
            .GroupBy(i => i.VariantId)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(row => row.VariantId, row => row.Quantity, ct);

    public Task<List<Guid>> GetSupplierIdsWithPendingOrders(CancellationToken ct = default)
        => _context.Set<PurchaseOrder>().AsNoTracking()
            .Where(p => p.Status == PurchaseStatusEnum.Pending)
            .Select(p => p.SupplierId).Distinct().ToListAsync(ct);
}
