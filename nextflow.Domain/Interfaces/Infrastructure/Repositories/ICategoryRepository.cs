using Nextflow.Domain.Dtos.Categories;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface ICategoryRepository : IBaseRepository<Category>
{
    /// <summary>Opções do formulário de produto (T2): as ativas e as já vinculadas, com a mãe logo antes das filhas.</summary>
    Task<List<CategorySummaryDto>> GetFormOptions(List<Guid> linkedIds, CancellationToken ct = default);
}
