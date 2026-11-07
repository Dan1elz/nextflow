using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ForecastRecommendationRepository(AppDbContext context) : BaseRepository<ForecastRecommendation>(context), IForecastRecommendationRepository;
