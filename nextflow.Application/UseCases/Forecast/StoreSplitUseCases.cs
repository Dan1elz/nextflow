using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Forecast;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>
/// Sugestão de divisão de uma recomendação "Comprar" pendente entre as lojas ativas (RN-DF27, F3.9). Por loja: participação nas
/// vendas do produto nos últimos <see cref="ShareWindowDays"/> dias, saldo, a caminho e a falta no horizonte (demanda projetada ×
/// participação − saldo − a caminho). Cada variação da recomendação é dividida pela falta (ou pela participação), no múltiplo do
/// fornecedor preferido. Loja sem local de recebimento padrão fica de fora. Só leitura.
/// </summary>
public class GetRecommendationStoreSplitUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IStoreRepository storeRepository,
    IStockBalanceRepository balanceRepository,
    IPurchaseItemRepository itemRepository,
    IOrderRepository orderRepository,
    IBusinessClock clock,
    IResultFactory<StoreSplitDto> resultFactory) : IGetRecommendationStoreSplitUseCase
{
    public const int ShareWindowDays = 90;

    public async Task<ResultDto<StoreSplitDto>> Execute(Guid recommendationId, CancellationToken ct = default)
    {
        var recommendation = await recommendationRepository.GetById(recommendationId,
            q => q.Include(r => r.Items).Include(r => r.Stores).Include(r => r.Product).ThenInclude(p => p.Suppliers), ct);
        if (recommendation is null)
            return resultFactory.Failure(ForecastRecommendationReading.NotFound, 404);
        if (recommendation.Action != RecommendationActionEnum.Buy)
            return resultFactory.Failure("Só recomendação \"Comprar\" divide a compra entre lojas.", 400);

        var stores = (await storeRepository.GetAll(s => s.IsActive && s.DefaultReceiveLocationId != null, 0, int.MaxValue,
                q => q.Include(s => s.Locations), q => q.OrderBy(s => s.Name), ct))
            .ToList();

        var productIds = new[] { recommendation.ProductId };
        var sold = await orderRepository.GetPaidQuantityByStore(recommendation.ProductId,
            clock.StartOfDayUtc(clock.Today.AddDays(-ShareWindowDays)), ct);
        var totalSold = sold.Values.Sum();

        var rows = new List<StoreSplitStoreDto>();
        foreach (var store in stores)
        {
            var share = totalSold > 0 ? Math.Round(sold.GetValueOrDefault(store.Id) / totalSold * 100m, 2, MidpointRounding.AwayFromZero) : 0m;
            var stock = (await balanceRepository.SumByProduct(productIds, store.Id, ct)).GetValueOrDefault(recommendation.ProductId);
            var incoming = (await itemRepository.SumIncomingByProduct(productIds, store.Id, ct)).GetValueOrDefault(recommendation.ProductId);
            var location = store.Locations.FirstOrDefault(l => l.Id == store.DefaultReceiveLocationId);
            // A divisão que a análise gravou (RN-DF27) vale sobre a recalculada: é o que a recomendação mostra.
            var analyzed = recommendation.Stores.FirstOrDefault(s => s.StoreId == store.Id);
            if (analyzed is not null)
                share = analyzed.SharePercent;
            rows.Add(new StoreSplitStoreDto
            {
                StoreId = store.Id,
                StoreName = store.Name,
                ReceiveLocationId = store.DefaultReceiveLocationId,
                ReceiveLocationName = location?.Name,
                SharePercent = share,
                CurrentStock = stock,
                IncomingStock = incoming,
                Shortfall = analyzed?.Shortfall ?? StoreSplitCalculator.Shortfall(recommendation.ProjectedDemand, share, stock, incoming),
            });
        }

        var multiple = recommendation.Product?.Suppliers.FirstOrDefault(s => s.IsPreferred)?.OrderMultiple;
        var inputs = rows.Select(r => new StoreSplitInput(r.StoreId, r.SharePercent, r.Shortfall)).ToList();
        var items = recommendation.Items.Where(i => i.Quantity > 0).Select(item => new StoreSplitItemDto
        {
            VariantId = item.VariantId,
            Quantity = item.Quantity,
            ByStore = [.. StoreSplitCalculator.Split(item.Quantity, inputs, multiple)
                .Select(part => new StoreSplitPartDto { StoreId = part.StoreId, Quantity = part.Quantity })],
        }).ToList();

        foreach (var row in rows)
            row.Quantity = items.Sum(i => i.ByStore.Where(p => p.StoreId == row.StoreId).Sum(p => p.Quantity));

        return resultFactory.Success(new StoreSplitDto
        {
            RecommendationId = recommendation.Id,
            OrderMultiple = multiple,
            Stores = rows,
            Items = items,
        });
    }
}
