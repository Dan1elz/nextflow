using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.DerivedData;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

/// <summary>
/// Herda a base para incluir e ler, mas recusa update e delete (RN-CE14). A interface própria não expõe os dois; os
/// overrides cobrem quem pedir o repositório genérico <see cref="IBaseRepository{TModel}"/>.
/// </summary>
public class StockMovementRepository(AppDbContext context) : BaseRepository<StockMovement>(context), IStockMovementRepository
{
    public const string ImmutableMessage = "Movimentação de estoque não é editada nem apagada: lance um movimento de correção.";

    public Task<List<MovementDeltaDto>> GetDeltasUntil(DateTime untilUtc, CancellationToken ct = default)
        => _context.Set<StockMovement>().AsNoTracking()
            .Where(m => m.CreatedAt <= untilUtc)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new MovementDeltaDto(m.VariantId, m.LocationId, m.CreatedAt, m.Quantity))
            .ToListAsync(ct);

    public override void Update(StockMovement model) => throw new InvalidOperationException(ImmutableMessage);

    public override void UpdateRange(IEnumerable<StockMovement> models) => throw new InvalidOperationException(ImmutableMessage);

    public override void Remove(StockMovement model) => throw new InvalidOperationException(ImmutableMessage);

    public override void RemoveRange(IEnumerable<StockMovement> models) => throw new InvalidOperationException(ImmutableMessage);
}
