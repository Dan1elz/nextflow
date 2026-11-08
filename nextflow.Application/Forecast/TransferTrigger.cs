namespace Nextflow.Application.Forecast;

/// <summary>Uma variação num local: disponível, mínimo efetivo (RN-CE33) e a demanda diária do local.</summary>
public sealed record LocationPosition(Guid VariantId, Guid LocationId, decimal Available, decimal Minimum, decimal DailyDemand);

/// <summary>Transferência sugerida: de um local (excesso) para outro (abaixo do mínimo), com a quantidade de cada variação.</summary>
public sealed record TransferPlan(Guid FromLocationId, Guid ToLocationId, IReadOnlyList<(Guid VariantId, decimal Quantity)> Items)
{
    public decimal Total => Items.Sum(i => i.Quantity);
}

/// <summary>
/// Gatilho do "Transferir" (RN-DF16), puro. Por variação, um local A abaixo do mínimo e um local B com excesso, em que excesso =
/// disponível − o maior entre o mínimo de B e a cobertura alvo × demanda diária de B (depósito tem demanda zero, então tudo
/// acima do mínimo dele é excesso). Quantidade = min(falta de A até o mínimo, excesso de B), arredondada para baixo nas casas da
/// unidade. Uma transferência por produto: o par de locais (origem, destino) que move mais, somando as variações.
/// </summary>
public static class TransferTrigger
{
    public static TransferPlan? Evaluate(IReadOnlyList<LocationPosition> positions, int coverageDays, int decimalPlaces)
    {
        var step = decimalPlaces <= 0 ? 1m : (decimal)Math.Pow(10, -decimalPlaces);
        decimal Floor(decimal value) => Math.Floor(value / step) * step;

        var byPair = new Dictionary<(Guid From, Guid To), List<(Guid VariantId, decimal Quantity)>>();
        foreach (var variant in positions.GroupBy(p => p.VariantId))
        {
            var lacking = variant.Where(p => p.Minimum > 0 && p.Available < p.Minimum).ToList();
            if (lacking.Count == 0)
                continue;

            var excess = variant
                .Select(p => (p.LocationId, Excess: p.Available - Math.Max(p.Minimum, coverageDays * p.DailyDemand)))
                .Where(x => x.Excess > 0)
                .ToDictionary(x => x.LocationId, x => x.Excess);

            foreach (var target in lacking.OrderBy(p => p.Available - p.Minimum))
            {
                var shortfall = target.Minimum - target.Available;
                foreach (var (from, available) in excess.Where(e => e.Key != target.LocationId).OrderByDescending(e => e.Value).ToList())
                {
                    var quantity = Floor(Math.Min(shortfall, available));
                    if (quantity <= 0)
                        continue;
                    var key = (from, target.LocationId);
                    if (!byPair.TryGetValue(key, out var items))
                        byPair[key] = items = [];
                    items.Add((variant.Key, quantity));
                    break;
                }
            }
        }

        if (byPair.Count == 0)
            return null;

        var best = byPair.OrderByDescending(p => p.Value.Sum(i => i.Quantity)).ThenBy(p => p.Key.From).ThenBy(p => p.Key.To).First();
        return new TransferPlan(best.Key.From, best.Key.To, best.Value);
    }
}
