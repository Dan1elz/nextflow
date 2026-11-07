using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories.Base;

namespace Nextflow.Infrastructure.Repositories;

public class PurchaseOrderRepository(AppDbContext context) : BaseRepository<PurchaseOrder>(context), IPurchaseOrderRepository
{
    public async Task<Dictionary<Guid, decimal>> GetIncomingStockByProductAsync(CancellationToken ct)
    {
        var rows = await _context.PurchaseItems
            .AsNoTracking()
            .Where(i => i.PurchaseOrder!.Active &&
                        (i.PurchaseOrder.PurchaseStatus == PurchaseStatus.Budget || i.PurchaseOrder.PurchaseStatus == PurchaseStatus.Pending))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.ProductId, r => (decimal)r.Quantity);
    }

    public async Task<List<PurchaseLeadTimeSampleDto>> GetReceivedLeadTimeSamplesAsync(DateTime sinceUtc, CancellationToken ct)
    {
        var since = DateTime.SpecifyKind(sinceUtc, DateTimeKind.Utc);

        return await _context.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.ReceivedAt != null && po.PurchaseStatus == PurchaseStatus.Received && po.CreateAt >= since)
            .Select(po => new PurchaseLeadTimeSampleDto
            {
                SupplierId = po.SupplierId,
                CreateAt = po.CreateAt,
                ReceivedAt = po.ReceivedAt!.Value
            })
            .ToListAsync(ct);
    }
}
