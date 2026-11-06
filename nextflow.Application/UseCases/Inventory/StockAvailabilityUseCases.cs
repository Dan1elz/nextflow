using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>
/// Aviso de falta (T-VD1a): onde a variação tem disponível (RN-CE08; sem orçamento, o físico) em locais ativos de lojas
/// ativas, sem o local excluído. Local zerado ou negativo fica fora. Mesma loja do local excluído primeiro, depois o maior
/// disponível.
/// </summary>
public class GetStockAvailabilityUseCase(
    IProductVariantRepository variantRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IResultFactory<StockAvailabilityDto> resultFactory) : IGetStockAvailabilityUseCase
{
    public async Task<ResultDto<StockAvailabilityDto>> Execute(Guid variantId, Guid? excludeLocationId, CancellationToken ct = default)
    {
        if (!await variantRepository.Exists(v => v.Id == variantId, ct))
            return resultFactory.Failure($"Entidade com id {variantId} não encontrada.", 404);

        Guid? referenceStoreId = excludeLocationId is { } excludedId
            ? (await locationRepository.GetById(excludedId, ct: ct))?.StoreId
            : null;

        var balances = await balanceRepository.GetAll(
            b => b.VariantId == variantId
                && b.Location.IsActive && b.Location.Store.IsActive
                && (excludeLocationId == null || b.LocationId != excludeLocationId),
            0,
            int.MaxValue,
            GetInclude(),
            ct: ct);

        var byLocation = balances
            // Por id: sem rastreamento, cada saldo (um por lote, a partir da F1.7) pode trazer a própria instância do local.
            .GroupBy(b => b.LocationId)
            .Select(g => (Location: g.First().Location, Available: g.Sum(b => b.Quantity)))
            .Select(row => new StockAvailabilityLocationDto
            {
                LocationId = row.Location.Id,
                LocationName = row.Location.Name,
                StoreId = row.Location.StoreId,
                StoreName = row.Location.Store != null ? row.Location.Store.Name : string.Empty,
                SameStore = referenceStoreId == row.Location.StoreId,
                Available = row.Available,
            })
            .Where(row => row.Available > 0)
            .OrderByDescending(row => row.SameStore)
            .ThenByDescending(row => row.Available)
            .ThenBy(row => row.StoreName)
            .ThenBy(row => row.LocationName)
            .ToList();

        return resultFactory.Success(new StockAvailabilityDto { VariantId = variantId, ByLocation = byLocation });
    }

    private static Func<IQueryable<StockBalance>, IQueryable<StockBalance>> GetInclude()
        => q => q.Include(b => b.Location).ThenInclude(l => l.Store);
}
