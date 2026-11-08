using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Projeções mensais por produto de cada análise (F3.2).</summary>
public interface IForecastProjectionRepository : IBaseRepository<ForecastProjection>
{
    /// <summary>
    /// Para cada produto e mês <b>completo</b> antes do mês de <paramref name="today"/>: a projeção da última análise concluída (com
    /// ou sem IA) cuja data de referência é anterior ao 1º dia do mês, junto com o vendido e os dias sem estoque (RN-DF19, F3.6b).
    /// Mês sem análise anterior ou sem venda mensal registrada não aparece; análise com falha ou em andamento é ignorada.
    /// </summary>
    Task<IReadOnlyList<ForecastAccuracyRowDto>> GetAccuracyRows(DateOnly today, CancellationToken ct = default);
}
