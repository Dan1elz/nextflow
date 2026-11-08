using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ForecastProjectionRepository(AppDbContext context) : BaseRepository<ForecastProjection>(context), IForecastProjectionRepository
{
    public async Task<IReadOnlyList<ForecastAccuracyRowDto>> GetAccuracyRows(DateOnly today, Guid? productId = null, CancellationToken ct = default)
    {
        var currentKey = (today.Year * 12) + today.Month;

        // Candidatas: projeção de análise concluída para um mês completo, com a análise referenciada antes do 1º dia desse mês.
        var candidates = await _context.Set<ForecastProjection>().AsNoTracking()
            .Where(p => p.ForecastRun.Status == ForecastRunStatusEnum.Completed
                || p.ForecastRun.Status == ForecastRunStatusEnum.CompletedWithoutAi)
            .Where(p => productId == null || p.ProductId == productId)
            .Where(p => (p.Year * 12) + p.Month < currentKey)
            .Where(p => p.ForecastRun.ReferenceDate < new DateOnly(p.Year, p.Month, 1))
            .Join(
                _context.Set<ProductSalesMonthly>(),
                p => new { p.ProductId, p.Year, p.Month },
                s => new { s.ProductId, s.Year, s.Month },
                (p, s) => new
                {
                    p.ProductId,
                    p.Year,
                    p.Month,
                    p.ProjectedQuantity,
                    p.ForecastRun.ReferenceDate,
                    p.ForecastRun.StartedAt,
                    s.QuantitySold,
                    s.DaysOutOfStock,
                })
            .ToListAsync(ct);

        // A última análise por (produto, mês) vence; empate na data de referência fica com a que começou depois.
        return candidates
            .GroupBy(c => (c.ProductId, c.Year, c.Month))
            .Select(g => g.OrderByDescending(c => c.ReferenceDate).ThenByDescending(c => c.StartedAt).First())
            .OrderBy(c => c.ProductId).ThenBy(c => c.Year).ThenBy(c => c.Month)
            .Select(c => new ForecastAccuracyRowDto(c.ProductId, c.Year, c.Month, c.ProjectedQuantity, c.QuantitySold, c.DaysOutOfStock))
            .ToList();
    }
}
