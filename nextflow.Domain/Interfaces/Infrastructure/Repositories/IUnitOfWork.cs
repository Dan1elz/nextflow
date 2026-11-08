namespace Nextflow.Domain.Interfaces.Infrastructure.Repositories;

/// <summary>Único ponto de commit: um <see cref="CommitAsync"/> por requisição (ADR 0010).</summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
    void Rollback();

    /// <summary>
    /// Exceção declarada ao "sem transação manual" do ADR 0010: só para o recálculo dos dados derivados (F3.1), que apaga e regrava
    /// tabelas inteiras e não pode deixá-las vazias se a gravação falhar. Venda, compra e estoque seguem com um commit só.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
