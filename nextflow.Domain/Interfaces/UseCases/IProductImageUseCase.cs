using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Utils;

namespace Nextflow.Domain.Interfaces.UseCases;

public interface IUpdateProductImageUseCase
{
    Task<ProductResponseDto> Execute(Guid productId, IFileData image, CancellationToken ct);
}

public interface IRemoveProductImageUseCase
{
    Task<ProductResponseDto> Execute(Guid productId, CancellationToken ct);
}
