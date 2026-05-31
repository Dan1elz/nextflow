using System;
using System.Threading;
using System.Threading.Tasks;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;

namespace Nextflow.Application.UseCases.Dashboard;

public class GetDashboardStatsUseCase(IDashboardRepository repository) : IGetDashboardStatsUseCase
{
    private readonly IDashboardRepository _repository = repository;

    public async Task<DashboardStatsDto> Execute(Guid userId, CancellationToken ct)
    {
        return await _repository.GetStatsAsync(userId, ct);
    }
}
