namespace Nextflow.Application.Forecast;

/// <summary>
/// Rateio da quantidade da recomendação nas variações pela proporção histórica de venda da grade (RN-DF21, RN-CE16). Variação sem
/// histórico (tamanho novo) entra com a média das que têm; sem nenhuma com histórico, a divisão é igual (L-DF5). A soma das
/// partes é exatamente a quantidade total, pelo método do maior resto, e cada parte respeita as casas decimais do produto.
/// </summary>
public static class VariantAllocator
{
    public static IReadOnlyList<VariantShare> Split(decimal totalQuantity, IReadOnlyList<(Guid VariantId, decimal HistoricalQuantity)> variants, int decimalPlaces)
    {
        if (variants.Count == 0 || totalQuantity <= 0)
            return [];

        var withHistory = variants.Where(v => v.HistoricalQuantity > 0).Select(v => v.HistoricalQuantity).ToList();
        var fallback = withHistory.Count > 0 ? withHistory.Average() : 1m;
        var weights = variants.Select(v => v.HistoricalQuantity > 0 ? v.HistoricalQuantity : fallback).ToList();
        var totalWeight = weights.Sum();

        var unit = (decimal)Math.Pow(10, Math.Clamp(decimalPlaces, 0, 3));
        var totalUnits = Math.Round(totalQuantity * unit, 0, MidpointRounding.AwayFromZero);

        var exact = weights.Select(w => totalUnits * w / totalWeight).ToList();
        var units = exact.Select(Math.Floor).ToList();
        var remainder = (int)(totalUnits - units.Sum());

        // Maior resto primeiro; empate fica com a variação de maior peso e, depois, com a primeira da grade.
        foreach (var index in Enumerable.Range(0, variants.Count)
                     .OrderByDescending(i => exact[i] - units[i])
                     .ThenByDescending(i => weights[i])
                     .ThenBy(i => i)
                     .Take(Math.Max(0, remainder)))
            units[index]++;

        return variants
            .Select((v, i) => new VariantShare(v.VariantId, units[i] / unit, Math.Round(weights[i] / totalWeight * 100m, 2, MidpointRounding.AwayFromZero)))
            .ToList();
    }
}
