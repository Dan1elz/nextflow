using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class UserStoreRepository(AppDbContext context) : BaseRepository<UserStore>(context), IUserStoreRepository
{
    public Task<List<Guid>> GetUserIds(Guid storeId, CancellationToken ct = default)
        => _context.Set<UserStore>().AsNoTracking().Where(us => us.StoreId == storeId).Select(us => us.UserId).ToListAsync(ct);

    public Task<List<Guid>> GetStoreIds(Guid userId, CancellationToken ct = default)
        => _context.Set<UserStore>().AsNoTracking().Where(us => us.UserId == userId).Select(us => us.StoreId).ToListAsync(ct);

    public Task<List<CurrentUserStoreDto>> GetActiveStores(Guid userId, CancellationToken ct = default)
        => _context.Set<UserStore>().AsNoTracking()
            .Where(us => us.UserId == userId && us.Store.IsActive)
            .OrderBy(us => us.Store.Name)
            .Select(us => new CurrentUserStoreDto(us.StoreId, us.Store.Name, us.Store.Locations.Count(l => l.IsActive)))
            .ToListAsync(ct);
}
