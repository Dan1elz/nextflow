using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class AddressRepository(AppDbContext context) : BaseRepository<Address>(context), IAddressRepository
{
    public async Task<Dictionary<Guid, string>> GetMainCityStates(
        OwnerTypeEnum ownerType, IReadOnlyCollection<Guid> ownerIds, CancellationToken ct = default)
    {
        var rows = await _context.Set<Address>().AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.IsMain && ownerIds.Contains(a.OwnerId))
            .Select(a => new { a.OwnerId, City = a.City.Name, State = a.City.State.Acronym })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.OwnerId).ToDictionary(g => g.Key, g => $"{g.First().City}/{g.First().State}");
    }

    public Task<List<Guid>> GetOwnerIdsByCity(OwnerTypeEnum ownerType, Guid cityId, CancellationToken ct = default)
        => _context.Set<Address>().AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.CityId == cityId)
            .Select(a => a.OwnerId)
            .Distinct()
            .ToListAsync(ct);
}
