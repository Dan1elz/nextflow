using FluentValidation.Results;
using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Único escritor de saldo e movimentação (RN-CE14). Recebe o documento de origem e as linhas, grava uma movimentação por
/// linha, atualiza o saldo (RN-CE01) e o custo médio da variação (RN-CE31). Não commita: quem commita é o use case chamador,
/// junto com o próprio documento. Concorrência no mesmo saldo estoura no commit e vira 409.
/// </summary>
public interface IStockLedger
{
    /// <summary>
    /// Devolve as falhas por campo (<c>Lines[i].Quantity</c> etc.) para o chamador responder 422 pela
    /// <c>IResultFactory</c>; com falha, nada é alterado. Lista vazia = lançado.
    /// </summary>
    Task<List<ValidationFailure>> Post(StockDocumentDto document, IReadOnlyList<StockLineDto> lines, CancellationToken ct);
}
