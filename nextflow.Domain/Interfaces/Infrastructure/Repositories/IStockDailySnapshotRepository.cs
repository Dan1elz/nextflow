using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Saldo diário derivado (F3.1): recriável, apagado por inteiro antes do recálculo.</summary>
public interface IStockDailySnapshotRepository : IBaseRepository<StockDailySnapshot>
{
    Task DeleteAllAsync(CancellationToken ct = default);
}
