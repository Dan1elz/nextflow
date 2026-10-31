using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ContactRepository(AppDbContext context) : BaseRepository<Contact>(context), IContactRepository
{
    public Task<List<Contact>> GetByOwners(OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default)
        => _context.Set<Contact>().AsNoTracking()
            .Where(c => c.OwnerType == ownerType && ownerIds.Contains(c.OwnerId))
            .OrderByDescending(c => c.IsMain).ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<List<Guid>> GetOwnerIdsByPhone(OwnerTypeEnum ownerType, string digits, CancellationToken ct = default)
        => _context.Set<Contact>().AsNoTracking()
            .Where(c => c.OwnerType == ownerType && c.Type != ContactTypeEnum.Email && c.Value.Contains(digits))
            .Select(c => c.OwnerId)
            .Distinct()
            .ToListAsync(ct);
}
