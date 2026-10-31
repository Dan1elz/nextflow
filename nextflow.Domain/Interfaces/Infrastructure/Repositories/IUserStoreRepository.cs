using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IUserStoreRepository : IBaseRepository<UserStore>
{
    Task<List<Guid>> GetUserIds(Guid storeId, CancellationToken ct = default);
    Task<List<Guid>> GetStoreIds(Guid userId, CancellationToken ct = default);
    Task<List<CurrentUserStoreDto>> GetActiveStores(Guid userId, CancellationToken ct = default);
}
