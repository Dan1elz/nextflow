using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Índice sazonal derivado (F3.1): recriável, apagado por inteiro antes do recálculo.</summary>
public interface IProductSeasonalityRepository : IBaseRepository<ProductSeasonality>
{
    Task DeleteAllAsync(CancellationToken ct = default);
}
