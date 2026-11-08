using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ForecastRecommendationRepository(AppDbContext context) : BaseRepository<ForecastRecommendation>(context), IForecastRecommendationRepository
{
    public async Task<Dictionary<Guid, ForecastRecommendationCountsDto>> GetCountsByRun(IReadOnlyCollection<Guid> runIds, CancellationToken ct = default)
    {
        if (runIds.Count == 0)
            return [];

        var rows = await _context.Set<ForecastRecommendation>().AsNoTracking()
            .Where(r => runIds.Contains(r.ForecastRunId))
            .GroupBy(r => new { r.ForecastRunId, r.Action, r.Status })
            .Select(g => new { g.Key.ForecastRunId, g.Key.Action, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.ForecastRunId)
            .ToDictionary(g => g.Key, g => new ForecastRecommendationCountsDto
            {
                Buy = g.Where(r => r.Action == RecommendationActionEnum.Buy).Sum(r => r.Count),
                Promote = g.Where(r => r.Action == RecommendationActionEnum.Promote).Sum(r => r.Count),
                Transfer = g.Where(r => r.Action == RecommendationActionEnum.Transfer).Sum(r => r.Count),
                Hold = g.Where(r => r.Action == RecommendationActionEnum.Hold).Sum(r => r.Count),
                Pending = g.Where(r => r.Status == RecommendationStatusEnum.Pending).Sum(r => r.Count),
                Applied = g.Where(r => r.Status == RecommendationStatusEnum.Applied).Sum(r => r.Count),
                Dismissed = g.Where(r => r.Status == RecommendationStatusEnum.Dismissed).Sum(r => r.Count),
                Expired = g.Where(r => r.Status == RecommendationStatusEnum.Expired).Sum(r => r.Count),
            });
    }
}
