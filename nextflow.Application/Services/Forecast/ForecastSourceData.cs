using Nextflow.Application.Forecast;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Tudo que o cálculo precisa, já lido do banco e reduzido a modelos puros: uma entrada por produto ativo, o histórico de venda por
/// variação (rateio, RN-DF21), a classe ABC calculada e os eventos do calendário. O horizonte ainda não existe aqui: ele sai do maior
/// prazo de entrega destas entradas (RN-DF17).
/// </summary>
public sealed class ForecastSourceData
{
    public required IReadOnlyList<ForecastProductInput> Inputs { get; init; }

    /// <summary>Por produto: as variações ativas com a quantidade vendida nos últimos 24 meses, na ordem da grade.</summary>
    public required IReadOnlyDictionary<Guid, IReadOnlyList<(Guid VariantId, decimal HistoricalQuantity)>> VariantHistory { get; init; }

    /// <summary>Casas decimais da quantidade de cada produto (a quantidade rateada respeita as casas).</summary>
    public required IReadOnlyDictionary<Guid, int> DecimalPlaces { get; init; }

    /// <summary>Classe ABC recalculada pela receita dos últimos 12 meses (RN-CE29).</summary>
    public required IReadOnlyDictionary<Guid, AbcClassEnum> AbcClasses { get; init; }

    public required IReadOnlyList<CalendarEvent> CalendarEvents { get; init; }
}
