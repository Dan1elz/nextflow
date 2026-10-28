using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>Histórico paginado de análises (mais recentes primeiro), com resumo e contagens.</summary>
public class GetAllForecastRunsUseCase(IForecastRunRepository repository)
    : GetAllUseCaseBase<ForecastRun, IForecastRunRepository, ForecastRunSummaryDto>(repository)
{
    protected override ForecastRunSummaryDto MapToResponseDto(ForecastRun entity) => new(entity);

    protected override Func<IQueryable<ForecastRun>, IQueryable<ForecastRun>>? GetInclude()
        => ForecastMappings.IncludeSummary;
}

/// <summary>Análise completa (com recomendações ordenadas) por id.</summary>
public class GetForecastRunByIdUseCase(IForecastRunRepository repository)
    : GetByIdUseCaseBase<ForecastRun, IForecastRunRepository, ForecastRunDto>(repository)
{
    public override async Task<ForecastRunDto> Execute(Guid id, CancellationToken ct)
    {
        var run = await _repository.GetByIdAsync(id, ct, GetInclude())
            ?? throw new Domain.Exceptions.NotFoundException("Análise de previsão não encontrada.");
        return MapToResponseDto(run);
    }

    protected override ForecastRunDto MapToResponseDto(ForecastRun entity) => new(entity);

    protected override Func<IQueryable<ForecastRun>, IQueryable<ForecastRun>>? GetInclude()
    {
        var today = BusinessClock.Today;
        return query => ForecastMappings.IncludeDetails(query, today);
    }
}

/// <summary>Última análise gerada (qualquer status) ou null quando nunca houve uma.</summary>
public class GetLatestForecastUseCase(IForecastRunRepository repository) : IGetLatestForecastUseCase
{
    public async Task<ForecastRunDto?> Execute(CancellationToken ct)
    {
        // GetAllAsync ordena por CreateAt desc → o primeiro é o mais recente
        var latest = (await repository.GetAllAsync(_ => true, 0, 1, ct)).FirstOrDefault();
        if (latest == null)
            return null;

        var today = BusinessClock.Today;
        var run = await repository.GetByIdAsync(latest.Id, ct, query => ForecastMappings.IncludeDetails(query, today));
        return run != null ? new ForecastRunDto(run) : null;
    }
}
