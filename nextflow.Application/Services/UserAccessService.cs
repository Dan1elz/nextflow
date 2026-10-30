using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.Services;

public class UserAccessService(IUserRepository userRepository) : IUserAccessService
{
    public Task<bool> CanAccess(Guid userId, CancellationToken ct = default)
        => userRepository.Exists(u => u.Id == userId && u.IsActive, ct);
}
