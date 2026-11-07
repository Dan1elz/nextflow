using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>Includes reutilizados pelos casos de uso de previsão para montar os DTOs do contrato.</summary>
internal static class ForecastMappings
{
    /// <summary>Usuário solicitante + recomendações (para nome e contagens do resumo).</summary>
    public static IQueryable<ForecastRun> IncludeSummary(IQueryable<ForecastRun> query) => query
        .Include(r => r.RequestedByUser)
        .Include(r => r.Recommendations)
        .AsSplitQuery();

    /// <summary>Resumo + produto (fornecedor e promoções vigentes) de cada recomendação.</summary>
    public static IQueryable<ForecastRun> IncludeDetails(IQueryable<ForecastRun> query, DateOnly today) => query
        .Include(r => r.RequestedByUser)
        .Include(r => r.Recommendations)
            .ThenInclude(rec => rec.Product)
                .ThenInclude(p => p!.Supplier)
        .Include(r => r.Recommendations)
            .ThenInclude(rec => rec.Product)
                .ThenInclude(p => p!.Promotions.Where(pr => pr.IsActive && pr.StartDate <= today && pr.EndDate >= today))
        .AsSplitQuery();

    /// <summary>Produto (fornecedor e promoções vigentes) + análise de origem.</summary>
    public static IQueryable<ForecastRecommendation> IncludeProduct(IQueryable<ForecastRecommendation> query, DateOnly today) => query
        .Include(rec => rec.ForecastRun)
        .Include(rec => rec.Product)
            .ThenInclude(p => p!.Supplier)
        .Include(rec => rec.Product)
            .ThenInclude(p => p!.Promotions.Where(pr => pr.IsActive && pr.StartDate <= today && pr.EndDate >= today))
        .AsSplitQuery();
}
