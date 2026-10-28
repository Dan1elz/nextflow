using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Utils;

public static class ProductQueryExtensions
{
    /// <summary>
    /// Inclui apenas as promoções vigentes na data informada (IsActive && StartDate &lt;= data &lt;= EndDate),
    /// necessárias para calcular <c>EffectivePrice</c>/<c>ActivePromotion</c>.
    /// Mantém a consulta única (comportamento padrão) de forma explícita: como não há sobreposição de promoções
    /// ativas, há no máximo uma vigente por produto e o JOIN extra não gera explosão cartesiana.
    /// </summary>
    public static IQueryable<Product> IncludeCurrentPromotions(this IQueryable<Product> query, DateOnly date)
        => query
            .Include(p => p.Promotions.Where(pr => pr.IsActive && pr.StartDate <= date && pr.EndDate >= date))
            .AsSingleQuery();
}
