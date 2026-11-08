using FluentValidation.Results;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Lança a transferência (RN-CE62) sem commitar: saída <c>TransferOut</c> na origem (por FEFO, com lote) e entrada
/// <c>TransferIn</c> no destino nos mesmos lotes, sem custo (o médio não muda). Usado pela transferência manual e pelo
/// "Aplicar Transferir" da previsão (F3.4b). Falhas vêm com o campo do pedido (<c>items[i].quantity</c>, <c>toLocationId</c>).
/// </summary>
public interface IStockTransferService
{
    Task<(StockTransfer? Transfer, List<ValidationFailure> Failures)> Transfer(
        StockTransferRequestDto dto, Guid userId, Guid? forecastRecommendationId, CancellationToken ct);
}
