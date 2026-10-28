namespace Nextflow.Domain.Interfaces.Repositories;

/// <summary>
/// Transação sobre o contexto de dados compartilhado pelos repositórios do escopo (request).
/// Usado quando um caso de uso precisa gravar em várias tabelas de forma atômica (tudo-ou-nada).
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Executa <paramref name="action"/> dentro de uma transação: commit ao final, rollback em caso de exceção.
    /// Se já houver uma transação aberta no contexto, apenas executa (participa da transação externa).
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
