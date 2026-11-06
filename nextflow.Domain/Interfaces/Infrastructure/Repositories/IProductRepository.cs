using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

public interface IProductRepository : IBaseRepository<Product>
{
    /// <summary>Produtos ativos em cada classe ABC (T13). Classe sem produto fica fora.</summary>
    Task<Dictionary<AbcClassEnum, int>> CountByAbcClass(CancellationToken ct = default);
}
