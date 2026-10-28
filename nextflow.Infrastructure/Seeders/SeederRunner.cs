using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Executa todos os <see cref="ISeeder"/> registrados, na ordem de <see cref="ISeeder.Order"/>.
/// Falha de um seeder interrompe o startup: subir a API com dados pela metade esconde o problema.
/// </summary>
public class SeederRunner(IEnumerable<ISeeder> seeders, ILogger<SeederRunner> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            var name = seeder.GetType().Name;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await seeder.SeedAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Seeder {Seeder} falhou.", name);
                throw;
            }

            logger.LogInformation("Seeder {Seeder} concluído em {ElapsedMs} ms.", name, stopwatch.ElapsedMilliseconds);
        }
    }
}
