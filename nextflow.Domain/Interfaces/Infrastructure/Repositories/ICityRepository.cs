using Nextflow.Domain.Dtos.Cities;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface ICityRepository : IBaseRepository<City>
{
    /// <summary>Nome e UF da cidade, ou <c>null</c> se ela não existe.</summary>
    Task<CitySummaryDto?> GetSummary(Guid id, CancellationToken ct = default);
}
