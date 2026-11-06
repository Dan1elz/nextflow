using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

/// <summary>Ajuste, perda ou saldo de abertura (T9), lançado pelo <c>IStockLedger</c>.</summary>
public interface ICreateManualMovementUseCase
{
    Task<ResultDto<bool>> Execute(ManualMovementRequestDto? dto, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Kardex (T8): movimentações paginadas, mais recente primeiro. Filtros: from, to (dia de negócio), storeId, locationId,
/// productId, variantId, reason, sourceType.
/// </summary>
public interface IGetStockMovementsUseCase
{
    Task<ResultDto<PaginationDto<StockMovementListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, CancellationToken ct = default);
}

/// <summary>Política de estoque (T13): as três classes ABC, na ordem A, B, C, com quantos produtos há em cada.</summary>
public interface IGetStockPoliciesUseCase
{
    Task<ResultDto<List<StockPolicyDto>>> Execute(CancellationToken ct = default);
}
