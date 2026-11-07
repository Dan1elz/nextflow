using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories.Base;

namespace Nextflow.Infrastructure.Repositories;

public class ProductSalesMonthlyRepository(AppDbContext context) : BaseRepository<ProductSalesMonthly>(context), IProductSalesMonthlyRepository
{
    public async Task<int> DeleteAllAsync(CancellationToken ct)
    {
        return await _context.ProductSalesMonthly.ExecuteDeleteAsync(ct);
    }

    public async Task<List<ProductMonthlySalesAggregateDto>> AggregateConfirmedSalesAsync(DateTime fromUtc, Guid? productId, CancellationToken ct)
    {
        var from = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc);

        var items = _context.OrderItems
            .AsNoTracking()
            .Where(i => i.Order!.Status == OrderStatus.PaymentConfirmed && i.Order.CreateAt >= from);

        if (productId.HasValue)
            items = items.Where(i => i.ProductId == productId.Value);

        // "CreateAt" AT TIME ZONE 'America/Sao_Paulo' → mês/ano locais calculados no próprio PostgreSQL
        return await items
            .Select(i => new
            {
                i.ProductId,
                i.OrderId,
                i.Quantity,
                LineRevenue = i.UnitPrice * i.Quantity - i.Discount,
                // Npgsql traduz para: "CreateAt" AT TIME ZONE 'America/Sao_Paulo'
                LocalDate = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(i.Order!.CreateAt, BusinessClock.TimeZoneId)
            })
            .GroupBy(x => new { x.ProductId, x.LocalDate.Year, x.LocalDate.Month })
            .Select(g => new ProductMonthlySalesAggregateDto
            {
                ProductId = g.Key.ProductId,
                Year = g.Key.Year,
                Month = g.Key.Month,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineRevenue),
                OrdersCount = g.Select(x => x.OrderId).Distinct().Count()
            })
            .ToListAsync(ct);
    }
}
