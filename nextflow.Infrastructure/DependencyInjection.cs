using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Infrastructure.Services;
using Nextflow.Infrastructure.Services.Forecast;
using Nextflow.Infrastructure.Services.Notifications;
using Nextflow.Infrastructure.Database;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Infrastructure.Seeders;
using Nextflow.Infrastructure.Seeders.Demo;
using Nextflow.Infrastructure.Storage;

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
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddStorage(configuration);

        // IA da previsão (ADR 0005): HttpClient tipado; sem Ai:ApiKey o cliente fica não configurado e a análise segue só com o cálculo.
        services.AddHttpClient<IAiClient, GeminiAiClient>();

        // Análise em segundo plano (RN-DF23) e agendamento semanal (RN-DF18).
        services.AddSingleton<IForecastQueue, ForecastQueue>();
        services.AddHostedService<ForecastRunWorker>();
        services.AddHostedService<ForecastScheduler>();
        // Resumo diário de estoque baixo e lote vencendo (L-DF4).
        services.AddHostedService<NotificationDigestScheduler>();

        services.Scan(scan => scan
            .FromAssemblyOf<UnitOfWork>()
            .AddClasses(classes => classes.AssignableTo<ISeeder>())
            .As<ISeeder>()
            .WithScopedLifetime());
        services.AddScoped<SeederRunner>();
        // Fotos do seed demo (Unsplash): prazo curto, foto que não baixa fica de fora e o startup segue.
        services.AddHttpClient<IDemoPhotoSource, UnsplashDemoPhotoSource>(client => client.Timeout = UnsplashDemoPhotoSource.Timeout);

        return services;
    }
}
