using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Health;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Health;

/// <summary>Saúde da API: responde sempre que o processo está de pé e informa se o banco está acessível.</summary>
public class GetHealthUseCase(
    IHealthRepository healthRepository,
    IBusinessClock clock,
    IResultFactory<HealthResponseDto> resultFactory) : IGetHealthUseCase
{
    public async Task<ResultDto<HealthResponseDto>> Execute(CancellationToken ct = default)
    {
        var database = await healthRepository.CanConnect(ct);
        return resultFactory.Success(new HealthResponseDto(database, clock.UtcNow));
    }
}
