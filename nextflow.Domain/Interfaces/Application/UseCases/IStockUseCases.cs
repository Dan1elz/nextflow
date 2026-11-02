using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>Ajuste, perda ou saldo de abertura (T9), lançado pelo <c>IStockLedger</c>.</summary>
public interface ICreateManualMovementUseCase
{
    Task<ResultDto<bool>> Execute(ManualMovementRequestDto? dto, Guid userId, CancellationToken ct = default);
}
