namespace Nextflow.Application.Forecast;

/// <summary>Loja candidata à divisão da compra: a participação nas vendas recentes e a falta calculada (RN-DF27).</summary>
public sealed record StoreSplitInput(Guid StoreId, decimal SharePercent, decimal Shortfall);

/// <summary>
/// Divisão da quantidade de uma compra entre lojas (RN-DF27), puro. Rateia pela falta de cada loja; sem falta em nenhuma, pela
/// participação nas vendas; sem nenhuma das duas, tudo na primeira loja (a da ordem recebida). Arredonda para baixo no múltiplo
/// do fornecedor (ou para inteiro, sem múltiplo) e o resto vai para a loja de maior peso, então a soma é sempre a quantidade.
/// </summary>
public static class StoreSplitCalculator
{
    public static List<(Guid StoreId, decimal Quantity)> Split(decimal quantity, IReadOnlyList<StoreSplitInput> stores, decimal? orderMultiple)
    {
        if (stores.Count == 0)
            return [];
        if (quantity <= 0)
            return [.. stores.Select(s => (s.StoreId, 0m))];

        var useShortfall = stores.Any(s => s.Shortfall > 0);
        var weights = stores.Select(s => useShortfall ? Math.Max(0m, s.Shortfall) : Math.Max(0m, s.SharePercent)).ToList();
        var total = weights.Sum();
        if (total == 0)
            return [.. stores.Select((s, i) => (s.StoreId, i == 0 ? quantity : 0m))];

        var step = orderMultiple is > 0 ? orderMultiple.Value : 1m;
        var parts = weights.Select(w => Math.Floor(quantity * w / total / step) * step).ToList();
        var heaviest = weights.IndexOf(weights.Max());
        parts[heaviest] += quantity - parts.Sum();

        return [.. stores.Select((s, i) => (s.StoreId, parts[i]))];
    }

    /// <summary>Falta da loja no horizonte: a parte dela na demanda menos o que já tem e o que está a caminho (nunca negativa).</summary>
    public static decimal Shortfall(decimal projectedDemand, decimal sharePercent, decimal currentStock, decimal incoming)
        => Math.Max(0m, Math.Round(projectedDemand * sharePercent / 100m - currentStock - incoming, 3, MidpointRounding.AwayFromZero));
}
