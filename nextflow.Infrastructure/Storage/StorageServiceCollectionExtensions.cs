using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Interfaces.Utils;

namespace Nextflow.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        services.AddSingleton<IStorageService, S3StorageService>();
        return services;
    }
}
