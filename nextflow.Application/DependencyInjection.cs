using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Application.Services;
using Nextflow.Application.Forecast;
using Nextflow.Application.Services.DerivedData;
using Nextflow.Application.Services.Forecast;
using Nextflow.Application.UseCases.Health;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped(typeof(IResultFactory<>), typeof(ResultFactory<>));
        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.AddScoped<IOwnerDirectory, OwnerDirectory>();
        services.AddScoped<IStoreMembership, StoreMembership>();
        services.AddScoped<IProductPricingService, ProductPricingService>();
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IDerivedDataService, DerivedDataService>();
        services.AddScoped<ForecastInputBuilder>();
        services.AddScoped<ForecastAiOrchestrator>();
        services.AddScoped<IForecastRunLauncher, ForecastRunLauncher>();
        services.AddScoped<IForecastRunProcessor, ForecastRunProcessor>();
        services.AddScoped<IForecastScheduleService, ForecastScheduleService>();
        services.AddScoped<ProductForecastReader>();
        services.AddScoped<IForecastEngine, ForecastEngine>();

        services.Scan(scan => scan
            .FromAssemblyOf<GetHealthUseCase>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("UseCase")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssemblyContaining<BaseModel>(ServiceLifetime.Scoped);

        return services;
    }
}
