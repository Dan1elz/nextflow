using Microsoft.EntityFrameworkCore;

namespace Nextflow.Infrastructure.Database;

/// <summary>Configuração do provider compartilhada pela API, pelo <c>dotnet ef</c> e pelos testes de integração.</summary>
public static class DatabaseOptions
{
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string? connectionString)
        => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
}
