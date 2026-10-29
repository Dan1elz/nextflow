using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nextflow.Infrastructure.Database;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Usada APENAS pelo "dotnet ef" no terminal local, contra o Postgres do compose.development.yaml (porta 5433).
        // Sobrescreva com a variável NEXTFLOW_DESIGN_CONNECTION se o seu banco local for outro.
        var connectionString = Environment.GetEnvironmentVariable("NEXTFLOW_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=nextflow_db;Username=nextflow_user;Password=nextflow_password";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        DatabaseOptions.Configure(optionsBuilder, connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
