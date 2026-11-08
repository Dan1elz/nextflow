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

    /// <summary>Por produto: cada variação × local com disponível e mínimo ("Transferir", RN-DF16).</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<Nextflow.Domain.Dtos.Stock.LocationPositionRowDto>> LocationPositions { get; init; }
        = new Dictionary<Guid, IReadOnlyList<Nextflow.Domain.Dtos.Stock.LocationPositionRowDto>>();

    /// <summary>"A caminho" (pedidos em Orçamento ou Pendente) por produto × loja.</summary>
    public IReadOnlyDictionary<(Guid ProductId, Guid StoreId), decimal> IncomingByStore { get; init; }
        = new Dictionary<(Guid ProductId, Guid StoreId), decimal>();

    /// <summary>Participação (0 a 1) de cada loja nas vendas do produto nos últimos 90 dias.</summary>
    public IReadOnlyDictionary<(Guid ProductId, Guid StoreId), decimal> StoreShares { get; init; }
        = new Dictionary<(Guid ProductId, Guid StoreId), decimal>();
}
