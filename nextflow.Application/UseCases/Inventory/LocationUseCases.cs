using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

internal static class InactiveStoreRule
{
    /// <summary>Loja inativa não recebe nem altera locais (RN-CE36).</summary>
    public const string Message = "A loja está inativa e não pode receber nem alterar locais.";
}

public class CreateLocationUseCase(
    IStockLocationRepository locationRepository,
    IStoreRepository storeRepository,
    IUnitOfWork unitOfWork,
    IValidator<LocationRequestDto> validator,
    IResultFactory<LocationResponseDto> resultFactory)
    : CreateUseCaseBase<StockLocation, IStockLocationRepository, LocationRequestDto, LocationResponseDto>(
        locationRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<LocationResponseDto>?> ValidateBusinessRules(LocationRequestDto dto, CancellationToken ct)
    {
        var store = await storeRepository.GetById(dto.StoreId, ct: ct);
        if (store is null)
            return _resultFactory.Failure("Loja não encontrada.", 404);

        return store.IsActive ? null : _resultFactory.Failure(InactiveStoreRule.Message, 400);
    }

    protected override StockLocation MapToEntity(LocationRequestDto dto) => new(dto);

    protected override LocationResponseDto MapToResponseDto(StockLocation entity) => new(entity);
}

public class UpdateLocationUseCase(
    IStockLocationRepository locationRepository,
    IStoreRepository storeRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateLocationRequestDto> validator,
    IResultFactory<LocationResponseDto> resultFactory)
    : UpdateUseCaseBase<StockLocation, IStockLocationRepository, UpdateLocationRequestDto, LocationResponseDto>(
        locationRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<LocationResponseDto>?> ValidateBusinessRules(StockLocation entity, UpdateLocationRequestDto dto, CancellationToken ct)
    {
        var store = await storeRepository.GetById(entity.StoreId, ct: ct);
        if (store is null || !store.IsActive)
            return _resultFactory.Failure(InactiveStoreRule.Message, 400);

        return !dto.IsSellable && store.DefaultSaleLocationId == entity.Id
            ? _resultFactory.FieldFailure(nameof(dto.IsSellable), "O local padrão de venda precisa ser vendável.")
            : null;
    }

    protected override Func<IQueryable<StockLocation>, IQueryable<StockLocation>> GetInclude()
        => q => q.Include(l => l.Balances.Where(b => b.Quantity != 0));

    protected override LocationResponseDto MapToResponseDto(StockLocation entity) => new(entity);
}

/// <summary>
/// Inativa o local. Padrão de venda ou recebimento não sai (decisão 7 de 29/10), a loja fica com ao menos um local ativo
/// e local com saldo diferente de zero não sai (RN-CE35): o saldo ficaria fora de todas as telas.
/// </summary>
public class DeleteLocationUseCase(
    IStockLocationRepository locationRepository,
    IStoreRepository storeRepository,
    IStockBalanceRepository balanceRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<StockLocation, IStockLocationRepository>(locationRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(StockLocation entity, Guid? userId, CancellationToken ct)
    {
        var store = await storeRepository.GetById(entity.StoreId, ct: ct);
        if (store is null || !store.IsActive)
            return _resultFactory.Failure(InactiveStoreRule.Message, 400);

        if (store.IsDefaultLocation(entity.Id))
            return _resultFactory.Failure("Este local é padrão de venda ou de recebimento da loja. Troque o padrão antes de inativar.", 400);

        var (id, storeId) = (entity.Id, entity.StoreId);
        if (await balanceRepository.Exists(b => b.LocationId == id && b.Quantity != 0, ct))
            return _resultFactory.Failure("Este local tem saldo de estoque. Zere ou transfira o saldo antes de inativar.", 422);

        return await _repository.Exists(l => l.StoreId == storeId && l.Id != id && l.IsActive, ct)
            ? null
            : _resultFactory.Failure("A loja precisa de ao menos um local ativo.", 400);
    }
}

public class ReactivateLocationUseCase(
    IStockLocationRepository locationRepository,
    IStoreRepository storeRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<StockLocation, IStockLocationRepository>(locationRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(StockLocation entity, Guid? userId, CancellationToken ct)
    {
        var store = await storeRepository.GetById(entity.StoreId, ct: ct);
        return store is { IsActive: true } ? null : _resultFactory.Failure(InactiveStoreRule.Message, 400);
    }
}

/// <summary>Combo de local: só ativos de loja ativa. Filtros: <c>storeId</c>, <c>isSellable</c>.</summary>
public class GetLocationOptionsUseCase(
    IStockLocationRepository locationRepository,
    IResultFactory<List<LocationOptionDto>> resultFactory)
    : GetOptionsUseCaseBase<StockLocation, IStockLocationRepository, LocationOptionDto>(locationRepository, resultFactory)
{
    protected override Expression<Func<StockLocation, bool>> BuildSearch(string term)
        => l => l.Name.ToLower().Contains(term) || l.Store.Name.ToLower().Contains(term);

    protected override void ApplyFilters(FilterExpressionBuilder<StockLocation> builder, FilterSet filters)
    {
        builder.And(l => l.Store.IsActive);
        builder.WhereGuidEquals(filters, "storeId", l => l.StoreId);
        builder.WhereBoolEquals(filters, "isSellable", l => l.IsSellable);
    }

    protected override Func<IQueryable<StockLocation>, IQueryable<StockLocation>> GetInclude() => q => q.Include(l => l.Store);

    protected override Func<IQueryable<StockLocation>, IOrderedQueryable<StockLocation>> GetOrderBy()
        => q => q.OrderBy(l => l.Store.Name).ThenBy(l => l.Name).ThenBy(l => l.Id);

    protected override LocationOptionDto MapToOption(StockLocation entity) => new(entity);
}
