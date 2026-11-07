using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Vendas mensais derivadas (F3.1): recriáveis, então o recálculo as apaga por inteiro antes de regravar.</summary>
public interface IProductSalesMonthlyRepository : IBaseRepository<ProductSalesMonthly>
{
    Task DeleteAllAsync(CancellationToken ct = default);
}
