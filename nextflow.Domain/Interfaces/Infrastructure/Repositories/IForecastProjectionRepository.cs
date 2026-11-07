using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Projeções mensais por produto de cada análise (F3.2).</summary>
public interface IForecastProjectionRepository : IBaseRepository<ForecastProjection>;
