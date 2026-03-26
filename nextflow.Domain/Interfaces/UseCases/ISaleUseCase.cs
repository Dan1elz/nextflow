using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.UseCases;

public interface ICreateSaleUseCase
{
    Task<SaleResponseDto> Execute(CreateSaleDto dto, CancellationToken ct);
}
public interface IDeleteSaleUseCase
{
    Task Execute(Guid id, CancellationToken ct, Guid? userId = null);
}