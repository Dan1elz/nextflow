using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Dados derivados da previsão (F3.1): vendas mensais por produto, saldo diário e índice sazonal. Só lê vendas pagas e o
/// livro-razão; nunca escreve saldo nem movimentação.
/// </summary>
public interface IDerivedDataService
{
    /// <summary>
    /// Recalcula tudo do zero e grava num único commit. Idempotente: rodar duas vezes dá o mesmo resultado, sem duplicar linhas.
    /// </summary>
    Task<DerivedDataRebuildDto> RebuildAsync(CancellationToken ct);
}
