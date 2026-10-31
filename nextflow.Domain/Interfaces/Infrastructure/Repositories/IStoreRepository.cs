using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IStoreRepository : IBaseRepository<Store>
{
    Task<List<Guid>> GetActiveIds(CancellationToken ct = default);
}
