using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>
/// Lotes (F1.7): só inclui e lê, como o livro-razão (RN-CE14). Lote não é editado nem apagado; o saldo dele é do
/// <c>StockBalance</c>.
/// </summary>
public interface ILotRepository
{
    Task AddAsync(Lot lot, CancellationToken ct = default);

    /// <summary>
    /// Lotes das variações, incluindo os criados nesta unidade de trabalho e ainda não gravados (o recebimento cria o lote e
    /// lança no livro-razão no mesmo commit).
    /// </summary>
    Task<List<Lot>> GetForVariants(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);
}
