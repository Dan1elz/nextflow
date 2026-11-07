using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Classe ABC pela receita dos últimos 12 meses (RN-CE29): A = produtos que somam os primeiros 80% da receita, B = os 15% seguintes,
/// C = os 5% restantes e os sem receita. O produto entra na classe em que a receita acumulada <b>antes</b> dele ainda não passou do
/// limite, então o produto que cruza os 80% continua A.
/// </summary>
public static class AbcClassifier
{
    public const decimal ClassALimit = 0.80m;
    public const decimal ClassBLimit = 0.95m;

    public static Dictionary<Guid, AbcClassEnum> Classify(IEnumerable<(Guid ProductId, decimal Revenue)> revenues)
    {
        var ordered = revenues.OrderByDescending(r => r.Revenue).ThenBy(r => r.ProductId).ToList();
        var total = ordered.Sum(r => Math.Max(0, r.Revenue));
        var classes = new Dictionary<Guid, AbcClassEnum>(ordered.Count);

        var accumulated = 0m;
        foreach (var (productId, revenue) in ordered)
        {
            if (revenue <= 0 || total <= 0)
            {
                classes[productId] = AbcClassEnum.C;
                continue;
            }

            var before = accumulated / total;
            classes[productId] = before < ClassALimit ? AbcClassEnum.A : before < ClassBLimit ? AbcClassEnum.B : AbcClassEnum.C;
            accumulated += revenue;
        }

        return classes;
    }
}
