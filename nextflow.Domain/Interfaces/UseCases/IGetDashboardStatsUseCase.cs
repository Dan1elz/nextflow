using System;
using System.Threading;
using System.Threading.Tasks;
using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.UseCases;

public interface IGetDashboardStatsUseCase
{
    Task<DashboardStatsDto> Execute(Guid userId, CancellationToken ct);
}
