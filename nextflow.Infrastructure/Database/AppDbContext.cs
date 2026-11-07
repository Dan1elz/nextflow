using Microsoft.EntityFrameworkCore;
using Nextflow.Infrastructure.Database.Configurations;

namespace Nextflow.Infrastructure.Database;

/// <summary>
/// Contexto único da API. Cada entidade tem sua <c>IEntityTypeConfiguration</c> em <c>Configurations/</c>; tabelas e
/// colunas em snake_case pela convenção registrada em <see cref="DatabaseOptions.Configure"/>.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Número do pedido (T-VD5): sequência única da empresa; um insert desfeito deixa buraco, o que é aceito.
        modelBuilder.HasSequence<int>(OrderConfiguration.NumberSequence);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
