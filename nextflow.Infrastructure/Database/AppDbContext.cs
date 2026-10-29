using Microsoft.EntityFrameworkCore;

namespace Nextflow.Infrastructure.Database;

/// <summary>
/// Contexto único da API. Cada entidade tem sua <c>IEntityTypeConfiguration</c> em <c>Configurations/</c>; tabelas e
/// colunas em snake_case pela convenção registrada em <see cref="DatabaseOptions.Configure"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
