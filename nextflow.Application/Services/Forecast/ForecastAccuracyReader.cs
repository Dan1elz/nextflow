using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>Precisão de um produto numa janela de meses completos, com o produto (nome, classe ABC e categorias) carregado.</summary>
public sealed record ProductAccuracyResult(Product Product, ProductAccuracy Accuracy);

/// <summary>
/// Lê a projeção de referência de cada mês completo (F3.6b) e mede a precisão com o <see cref="ForecastAccuracyCalculator"/> (F3.6a),
/// para as rotas da T-DF10 e a aba "Previsão" do produto (T-DF5). Só leitura; a janela são os últimos N meses completos antes do mês
/// corrente (RN-DF19).
/// </summary>
public class ForecastAccuracyReader(
    IForecastProjectionRepository projectionRepository,
    IProductRepository productRepository,
    IBusinessClock clock)
{
    public const int DefaultMonths = 12;
    public const int MaxMonths = 36;

    /// <summary>Os N meses completos da janela, do mais antigo ao mais recente.</summary>
    public IReadOnlyList<YearMonth> Window(int months)
    {
        months = Math.Clamp(months <= 0 ? DefaultMonths : months, 1, MaxMonths);
        var current = YearMonth.From(clock.Today).FirstDay;
        return [.. Enumerable.Range(1, months).Reverse().Select(i => YearMonth.From(current.AddMonths(-i)))];
    }

    /// <summary>Precisão de cada produto com projeção de referência na janela (produto sem nenhuma linha não aparece).</summary>
    public async Task<IReadOnlyList<ProductAccuracyResult>> LoadAsync(IReadOnlyList<YearMonth> window, CancellationToken ct)
    {
        var observations = await ObservationsAsync(window, null, ct);
        if (observations.Count == 0)
            return [];

        var ids = observations.Keys.ToList();
        var products = await productRepository.GetAll(
            p => ids.Contains(p.Id), 0, int.MaxValue,
            q => q.Include(p => p.Categories).ThenInclude(link => link.Category).ThenInclude(c => c.Parent),
            ct: ct);

        return [.. products.Select(p => new ProductAccuracyResult(p, ForecastAccuracyCalculator.MeasureProduct(observations[p.Id])))];
    }

    /// <summary>Meses da janela de um produto, medidos ou não (erro nulo quando o mês ficou fora).</summary>
    public async Task<IReadOnlyList<AccuracyMonth>> LoadProductAsync(Guid productId, IReadOnlyList<YearMonth> window, CancellationToken ct)
    {
        var observations = await ObservationsAsync(window, productId, ct);
        return observations.TryGetValue(productId, out var rows) ? ForecastAccuracyCalculator.MeasureProduct(rows).Months : [];
    }

    private async Task<Dictionary<Guid, List<AccuracyObservation>>> ObservationsAsync(IReadOnlyList<YearMonth> window, Guid? productId, CancellationToken ct)
    {
        var inWindow = window.ToHashSet();
        var rows = await projectionRepository.GetAccuracyRows(clock.Today, productId, ct);
        return rows
            .Select(r => (r.ProductId, Observation: new AccuracyObservation(new YearMonth(r.Year, r.Month), r.Projected, r.Actual, r.DaysOutOfStock)))
            .Where(r => inWindow.Contains(r.Observation.Month))
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Observation).ToList());
    }
}
