using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IUserRepository : IBaseRepository<User>
{
    Task<List<Guid>> GetActiveAdminIds(CancellationToken ct = default);
}
