using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Health;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

public interface IGetHealthUseCase
{
    Task<ResultDto<HealthResponseDto>> Execute(CancellationToken ct = default);
}
