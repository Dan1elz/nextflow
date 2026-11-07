using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Calcula os dados derivados da previsão (F3.1) depois do histórico de 36 meses do demo. Só roda com o histórico ligado e as
/// tabelas vazias; depois disso o recálculo é por <see cref="IDerivedDataService"/> (agendamento semanal na F3.3).
/// </summary>
public class DerivedDataSeeder(
    IConfiguration configuration,
    IDerivedDataService derivedDataService,
    IProductSalesMonthlyRepository salesMonthlyRepository,
    ILogger<DerivedDataSeeder> logger) : ISeeder
{
    // Depois do DemoHistorySeeder (60), que cria as vendas e as movimentações.
    public int Order => 70;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (DemoHistorySeeder.HistoryMonths(configuration) == 0)
            return;

        if (await salesMonthlyRepository.Exists(_ => true, ct))
            return;

        var stopwatch = Stopwatch.StartNew();
        var result = await derivedDataService.RebuildAsync(ct);

        logger.LogInformation(
            "Dados derivados: {Sales} meses de venda, {Snapshots} saldos diários e {Seasonality} índices sazonais em {Seconds} s.",
            result.SalesMonthlyRows, result.SnapshotRows, result.SeasonalityRows, stopwatch.Elapsed.TotalSeconds.ToString("0"));
    }
}
