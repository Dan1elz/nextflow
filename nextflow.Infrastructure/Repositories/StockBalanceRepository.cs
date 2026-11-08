using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Dashboard;
using Nextflow.Domain.Dtos.Forecasts;
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

    public async Task<List<ExpiringLotBalanceDto>> GetLotBalances(CancellationToken ct = default)
    {
        var rows = await _context.Set<StockBalance>().AsNoTracking()
            .Where(b => b.LotId != null && b.Quantity > 0)
            .GroupBy(b => new { b.VariantId, b.Lot!.ExpiresOn })
            .Select(g => new { g.Key.VariantId, g.Key.ExpiresOn, Quantity = g.Sum(b => b.Quantity) })
            .ToListAsync(ct);
        return rows.Select(r => new ExpiringLotBalanceDto(r.VariantId, r.ExpiresOn, r.Quantity)).ToList();
    }

    public async Task<(int Count, List<LowStockRowDto> Top)> GetLowStock(Guid? storeId, int top, CancellationToken ct = default)
    {
        var variants = _context.Set<ProductVariant>().AsNoTracking().Where(v => v.IsActive && v.RemovedOn == null && v.Product.IsActive);
        var locations = _context.Set<StockLocation>().AsNoTracking()
            .Where(l => l.IsActive && l.Store.IsActive && (storeId == null || l.StoreId == storeId));
        var balances = _context.Set<StockBalance>().AsNoTracking();
        var minimums = _context.Set<StockLocationMinimum>().AsNoTracking();

        // Mesma regra do estoque baixo da listagem: sem saldo, disponível zero; sem mínimo do local, o padrão do produto.
        var below =
            from variant in variants
            from location in locations
            let available = balances
                .Where(b => b.VariantId == variant.Id && b.LocationId == location.Id)
                .Sum(b => (decimal?)b.Quantity) ?? 0m
            let minimum = minimums
                .Where(m => m.VariantId == variant.Id && m.LocationId == location.Id)
                .Select(m => (decimal?)m.MinQuantity)
                .FirstOrDefault() ?? variant.Product.DefaultMinQuantity
            where available < minimum
            select new { variant.ProductId, ProductName = variant.Product.Name, VariantId = variant.Id, LocationName = location.Name, Available = available, Minimum = minimum };

        var count = await below.CountAsync(ct);
        var rows = await below
            .OrderBy(r => r.Available - r.Minimum)
            .ThenBy(r => r.ProductName)
            .Take(top)
            .ToListAsync(ct);
        return (count, [.. rows.Select(r => new LowStockRowDto(r.ProductId, r.ProductName, r.VariantId, r.LocationName, r.Available, r.Minimum))]);
    }

    public async Task<List<ExpiringLotRowDto>> GetExpiringLots(DateOnly from, DateOnly until, Guid? storeId, CancellationToken ct = default)
    {
        var rows = await _context.Set<StockBalance>().AsNoTracking()
            .Where(b => b.LotId != null && b.Quantity > 0
                && b.Lot!.ExpiresOn >= from && b.Lot.ExpiresOn <= until
                && b.Location.IsActive && b.Location.Store.IsActive
                && (storeId == null || b.Location.StoreId == storeId))
            .Select(b => new
            {
                b.VariantId,
                ProductName = b.Variant.Product.Name,
                LotCode = b.Lot!.Code,
                LocationName = b.Location.Name,
                b.Lot.ExpiresOn,
                b.Quantity,
                b.Variant.AverageCost,
            })
            .ToListAsync(ct);
        return [.. rows.Select(r => new ExpiringLotRowDto(r.VariantId, r.ProductName, r.LotCode, r.LocationName, r.ExpiresOn, r.Quantity, r.AverageCost))];
    }

    public async Task<HashSet<Guid>> GetLowStockProductIds(IReadOnlyCollection<Guid>? productIds, Guid? storeId, CancellationToken ct = default)
    {
        var variants = _context.Set<ProductVariant>().AsNoTracking()
            .Where(v => v.IsActive && v.RemovedOn == null && v.Product.IsActive
                && (productIds == null || productIds.Contains(v.ProductId)));
        var locations = _context.Set<StockLocation>().AsNoTracking()
            .Where(l => l.IsActive && l.Store.IsActive && (storeId == null || l.StoreId == storeId));
        var balances = _context.Set<StockBalance>().AsNoTracking();
        var minimums = _context.Set<StockLocationMinimum>().AsNoTracking();

        // Variação × local: sem saldo gravado, o disponível é zero; sem mínimo do local, vale o padrão do produto.
        // Sem orçamento ainda, disponível = físico (RN-CE08).
        var ids = await (
            from variant in variants
            from location in locations
            let available = balances
                .Where(b => b.VariantId == variant.Id && b.LocationId == location.Id)
                .Sum(b => (decimal?)b.Quantity) ?? 0m
            let minimum = minimums
                .Where(m => m.VariantId == variant.Id && m.LocationId == location.Id)
                .Select(m => (decimal?)m.MinQuantity)
                .FirstOrDefault() ?? variant.Product.DefaultMinQuantity
            where available < minimum
            select variant.ProductId)
            .Distinct()
            .ToListAsync(ct);

        return [.. ids];
    }
}
