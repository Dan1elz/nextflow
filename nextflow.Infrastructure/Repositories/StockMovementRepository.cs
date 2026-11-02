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

    public override void Update(StockMovement model) => throw new InvalidOperationException(ImmutableMessage);

    public override void UpdateRange(IEnumerable<StockMovement> models) => throw new InvalidOperationException(ImmutableMessage);

    public override void Remove(StockMovement model) => throw new InvalidOperationException(ImmutableMessage);

    public override void RemoveRange(IEnumerable<StockMovement> models) => throw new InvalidOperationException(ImmutableMessage);
}
