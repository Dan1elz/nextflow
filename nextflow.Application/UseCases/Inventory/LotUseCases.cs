using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>Janela do alerta de vencimento (F1.7): lote que vence em até tantos dias, contando hoje.</summary>
public static class LotAlert
{
    public const int ExpiringSoonDays = 30;
}

/// <summary>
/// Aba Lotes do T3 (F1.7): saldo de cada lote do produto por local ativo de loja ativa (da loja informada ou de todas), sem
/// os zerados. Do que vence primeiro ao último, a mesma ordem da baixa (FEFO, RN-CE15). Dias para vencer contam do dia de
/// negócio de hoje (<see cref="IBusinessClock"/>).
/// </summary>
public class GetProductLotsUseCase(
    IProductRepository productRepository,
    IStockBalanceRepository balanceRepository,
    IBusinessClock clock,
    IResultFactory<List<LotBalanceDto>> resultFactory) : IGetProductLotsUseCase
{
    public async Task<ResultDto<List<LotBalanceDto>>> Execute(Guid id, Guid? storeId, CancellationToken ct = default)
    {
        if (!await productRepository.Exists(p => p.Id == id, ct))
            return resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        var balances = await balanceRepository.GetAll(
            b => b.Variant.ProductId == id && b.LotId != null && b.Quantity != 0
                && b.Location.IsActive && b.Location.Store.IsActive && (storeId == null || b.Location.StoreId == storeId),
            0,
            int.MaxValue,
            q => q.Include(b => b.Lot)
                .Include(b => b.Location).ThenInclude(l => l.Store)
                .Include(b => b.Variant).ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute),
            q => q.OrderBy(b => b.Lot!.ExpiresOn).ThenBy(b => b.Lot!.Code).ThenBy(b => b.Location.Store.Name).ThenBy(b => b.Location.Name),
            ct);

        var today = clock.Today;
        return resultFactory.Success([.. balances.Select(b => ToDto(b, today))]);
    }

    private static LotBalanceDto ToDto(StockBalance balance, DateOnly today)
    {
        var days = balance.Lot!.ExpiresOn.DayNumber - today.DayNumber;
        return new LotBalanceDto
        {
            LotId = balance.Lot.Id,
            Code = balance.Lot.Code,
            ExpiresOn = balance.Lot.ExpiresOn,
            DaysToExpire = days,
            IsExpired = days < 0,
            IsExpiringSoon = days >= 0 && days <= LotAlert.ExpiringSoonDays,
            VariantId = balance.VariantId,
            VariantLabel = ProductVariantResponseDto.LabelOf(balance.Variant),
            LocationId = balance.LocationId,
            LocationName = balance.Location.Name,
            StoreName = balance.Location.Store.Name,
            Quantity = balance.Quantity,
        };
    }
}

/// <summary>
/// Lotes para o ajuste manual (T9, F1.7): todos os da variação, com o saldo no local (zero onde o lote não tem saldo, para
/// uma entrada poder repor um lote que já existe). Do que vence primeiro ao último.
/// </summary>
public class GetLotOptionsUseCase(
    IProductVariantRepository variantRepository,
    ILotRepository lotRepository,
    IStockBalanceRepository balanceRepository,
    IResultFactory<List<LotOptionDto>> resultFactory) : IGetLotOptionsUseCase
{
    public async Task<ResultDto<List<LotOptionDto>>> Execute(Guid variantId, Guid locationId, CancellationToken ct = default)
    {
        if (!await variantRepository.Exists(v => v.Id == variantId, ct))
            return resultFactory.Failure($"Entidade com id {variantId} não encontrada.", 404);

        var lots = await lotRepository.GetForVariants([variantId], ct);
        var balances = (await balanceRepository.GetAll(
                b => b.VariantId == variantId && b.LocationId == locationId && b.LotId != null, 0, int.MaxValue, ct: ct))
            .ToDictionary(b => b.LotId!.Value, b => b.Quantity);

        return resultFactory.Success([.. lots
            .OrderBy(l => l.ExpiresOn).ThenBy(l => l.Code)
            .Select(l => new LotOptionDto { Id = l.Id, Code = l.Code, ExpiresOn = l.ExpiresOn, Quantity = balances.GetValueOrDefault(l.Id) })]);
    }
}
