using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Repositories;

public class HealthRepository(AppDbContext context) : IHealthRepository
{
    public async Task<bool> CanConnect(CancellationToken ct = default)
    {
        try
        {
            return await context.Database.CanConnectAsync(ct);
        }
        catch (Exception)
        {
            // Saúde nunca derruba a requisição: banco fora do ar vira "Degraded".
            return false;
        }
    }
}
