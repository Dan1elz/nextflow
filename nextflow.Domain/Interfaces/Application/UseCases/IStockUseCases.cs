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

/// <summary>Aviso de falta (T-VD1a): locais ativos com disponível da variação, sem o local excluído (o de venda).</summary>
public interface IGetStockAvailabilityUseCase
{
    Task<ResultDto<StockAvailabilityDto>> Execute(Guid variantId, Guid? excludeLocationId, CancellationToken ct = default);
}

/// <summary>Aba Lotes do T3 (F1.7): saldo por lote e local do produto, da loja informada ou de todas.</summary>
public interface IGetProductLotsUseCase
{
    Task<ResultDto<List<LotBalanceDto>>> Execute(Guid id, Guid? storeId, CancellationToken ct = default);
}

/// <summary>Lotes da variação com o saldo no local, para o ajuste manual (T9, F1.7).</summary>
public interface IGetLotOptionsUseCase
{
    Task<ResultDto<List<LotOptionDto>>> Execute(Guid variantId, Guid locationId, CancellationToken ct = default);
}

/// <summary>Transferência de estoque entre locais (RN-CE62).</summary>
public interface ICreateStockTransferUseCase
{
    Task<ResultDto<StockTransferDto>> Execute(StockTransferRequestDto? dto, Guid userId, CancellationToken ct = default);
}

public interface IGetStockTransferUseCase
{
    Task<ResultDto<StockTransferDto>> Execute(Guid id, CancellationToken ct = default);
}
