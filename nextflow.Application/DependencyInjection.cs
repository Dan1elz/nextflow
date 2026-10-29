using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Application.Services;
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

        services.Scan(scan => scan
            .FromAssemblyOf<GetHealthUseCase>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("UseCase")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssemblyContaining<BaseModel>(ServiceLifetime.Scoped);

        return services;
    }
}
