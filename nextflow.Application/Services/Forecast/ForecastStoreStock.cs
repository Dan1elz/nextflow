using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.Services.Forecast;

/// <summary>
/// Estoque da loja selecionada no topo ao lado dos números da análise. A análise é da empresa inteira (o histórico de vendas não
/// guarda loja), então a quantidade sugerida não muda; só o saldo e o "a caminho" daquela loja são lidos agora, na hora da consulta.
/// </summary>
public static class ForecastStoreStock
{
    public static async Task FillAsync(
        IReadOnlyCollection<ForecastRecommendationListItemDto> rows, Guid? storeId,
        IStockBalanceRepository balanceRepository, IPurchaseItemRepository itemRepository, CancellationToken ct)
    {
        if (storeId is not { } store || rows.Count == 0)
            return;

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var stock = await balanceRepository.SumByProduct(productIds, store, ct);
        var incoming = await itemRepository.SumIncomingByProduct(productIds, store, ct);

        foreach (var row in rows)
        {
            row.StoreCurrentStock = stock.GetValueOrDefault(row.ProductId);
            row.StoreIncomingStock = incoming.GetValueOrDefault(row.ProductId);
        }
    }
}
