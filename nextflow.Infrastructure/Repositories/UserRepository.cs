using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : BaseRepository<User>(context), IUserRepository
{
    public Task<List<Guid>> GetActiveAdminIds(CancellationToken ct = default)
        => _context.Set<User>().AsNoTracking()
            .Where(u => u.IsActive && u.Role == RoleEnum.Admin)
            .Select(u => u.Id)
            .ToListAsync(ct);
}
