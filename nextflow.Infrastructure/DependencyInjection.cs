using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Infrastructure.Seeders;

namespace Nextflow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options => DatabaseOptions.Configure(options, connectionString));

        services.Scan(scan => scan
            .FromAssemblyOf<UnitOfWork>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Repository")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.Scan(scan => scan
            .FromAssemblyOf<UnitOfWork>()
            .AddClasses(classes => classes.AssignableTo<ISeeder>())
            .As<ISeeder>()
            .WithScopedLifetime());
        services.AddScoped<SeederRunner>();

        return services;
    }
}
