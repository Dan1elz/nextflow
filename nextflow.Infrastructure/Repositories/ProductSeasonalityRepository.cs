using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories.Base;

namespace Nextflow.Infrastructure.Repositories;

public class ProductSeasonalityRepository(AppDbContext context) : BaseRepository<ProductSeasonality>(context), IProductSeasonalityRepository
{ }
