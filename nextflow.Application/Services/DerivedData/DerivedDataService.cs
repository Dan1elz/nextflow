using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.DerivedData;

/// <summary>
/// Recálculo dos dados derivados da previsão (F3.1). Lê vendas pagas e movimentações, calcula tudo em memória pelo
/// <see cref="DerivedDataCalculator"/> e grava as três tabelas num único commit, dentro de uma transação. Nunca escreve saldo nem movimentação.
/// </summary>
public class DerivedDataService(
    IOrderRepository orderRepository,
    IStockMovementRepository movementRepository,
    IProductVariantRepository variantRepository,
    IStockLocationRepository locationRepository,
    IProductSalesMonthlyRepository salesMonthlyRepository,
    IStockDailySnapshotRepository snapshotRepository,
    IProductSeasonalityRepository seasonalityRepository,
    IUnitOfWork unitOfWork,
    IBusinessClock clock) : IDerivedDataService
{
    public async Task<DerivedDataRebuildDto> RebuildAsync(CancellationToken ct)
    {
        var today = clock.Today;

        var lines = await orderRepository.GetPaidSaleLines(ct);
        var deltas = await movementRepository.GetDeltasUntil(clock.UtcNow, ct);
        var productByVariant = (await variantRepository.GetAll(_ => true, 0, int.MaxValue, ct: ct))
            .ToDictionary(v => v.Id, v => v.ProductId);
        var sellableLocations = (await locationRepository.GetAll(l => l.IsSellable && l.IsActive, 0, int.MaxValue, ct: ct))
            .Select(l => l.Id)
            .ToHashSet();

        var snapshots = DerivedDataCalculator.DailySnapshots(deltas, clock.ToBusinessDate, today);
        var monthly = DerivedDataCalculator.MonthlySales(lines, snapshots, productByVariant, sellableLocations, clock.ToBusinessDate);
        var seasonality = DerivedDataCalculator.Seasonality(monthly, today);

        // Tabelas derivadas: apaga tudo e regrava, tudo numa transação (se a gravação falhar, as tabelas antigas continuam).
        // Rodar de novo dá o mesmo resultado, sem linha duplicada.
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await salesMonthlyRepository.DeleteAllAsync(token);
            await snapshotRepository.DeleteAllAsync(token);
            await seasonalityRepository.DeleteAllAsync(token);

            await salesMonthlyRepository.AddRangeAsync(monthly.Select(m => new ProductSalesMonthly(m)), token);
            await snapshotRepository.AddRangeAsync(snapshots.Select(s => new StockDailySnapshot(s)), token);
            await seasonalityRepository.AddRangeAsync(seasonality.Select(s => new ProductSeasonality(s)), token);
            await unitOfWork.CommitAsync(token);
        }, ct);

        return new DerivedDataRebuildDto(monthly.Count, snapshots.Count, seasonality.Count);
    }
}
