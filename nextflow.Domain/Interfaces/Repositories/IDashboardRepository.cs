using System;
using System.Threading;
using System.Threading.Tasks;
using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.Repositories;

public interface IDashboardRepository
{
    Task<DashboardStatsDto> GetStatsAsync(Guid userId, CancellationToken ct);
}
