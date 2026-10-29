namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Verificação de conectividade do banco para o endpoint de saúde.</summary>
public interface IHealthRepository
{
    Task<bool> CanConnect(CancellationToken ct = default);
}
