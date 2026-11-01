using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class CategoryRepository(AppDbContext context) : BaseRepository<Category>(context), ICategoryRepository
{
    public Task<List<CategorySummaryDto>> GetFormOptions(List<Guid> linkedIds, CancellationToken ct = default)
        => _context.Set<Category>().AsNoTracking()
            .Where(c => c.IsActive || linkedIds.Contains(c.Id))
            .OrderBy(c => c.Parent != null ? c.Parent.Name : c.Name)
            .ThenBy(c => c.ParentId != null)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new CategorySummaryDto(c.Id, c.Name, c.ParentId, c.Parent != null ? c.Parent.Name : null))
            .ToListAsync(ct);
}
