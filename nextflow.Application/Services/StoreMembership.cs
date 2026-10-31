using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

public class StoreMembership(
    IUserStoreRepository userStoreRepository,
    IUserRepository userRepository,
    IStoreRepository storeRepository) : IStoreMembership
{
    public async Task LinkStore(Guid storeId, CancellationToken ct = default)
    {
        var linked = await userStoreRepository.GetUserIds(storeId, ct);
        var admins = await userRepository.GetActiveAdminIds(ct);

        await userStoreRepository.AddRangeAsync(admins.Except(linked).Select(userId => new UserStore(userId, storeId)), ct);
    }

    public async Task LinkUser(Guid userId, CancellationToken ct = default)
    {
        var linked = await userStoreRepository.GetStoreIds(userId, ct);
        var stores = await storeRepository.GetActiveIds(ct);

        await userStoreRepository.AddRangeAsync(stores.Except(linked).Select(storeId => new UserStore(userId, storeId)), ct);
    }

    public Task<List<CurrentUserStoreDto>> GetStoresOf(Guid userId, CancellationToken ct = default)
        => userStoreRepository.GetActiveStores(userId, ct);
}
