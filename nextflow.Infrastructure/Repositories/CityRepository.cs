using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Cities;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class CityRepository(AppDbContext context) : BaseRepository<City>(context), ICityRepository
{
    public Task<CitySummaryDto?> GetSummary(Guid id, CancellationToken ct = default)
        => _context.Set<City>().AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CitySummaryDto(c.Name, c.State.Acronym))
            .FirstOrDefaultAsync(ct);
}
