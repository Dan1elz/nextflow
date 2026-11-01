using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class ProductVariantRepository(AppDbContext context) : BaseRepository<ProductVariant>(context), IProductVariantRepository;
