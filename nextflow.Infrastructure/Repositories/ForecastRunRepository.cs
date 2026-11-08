using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ForecastRunRepository(AppDbContext context) : BaseRepository<ForecastRun>(context), IForecastRunRepository;
