using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Lotes enviados à IA em cada análise (F3.2, RN-DF20).</summary>
public interface IForecastAiBatchRepository : IBaseRepository<ForecastAiBatch>;
