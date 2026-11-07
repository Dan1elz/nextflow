using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>
/// Aplica recomendações "Comprar": agrupa os itens por fornecedor e cria um pedido de compra (status Orçamento)
/// por fornecedor reutilizando o caso de uso de pedidos de compra; marca as recomendações como aplicadas.
/// Tudo-ou-nada (transação).
/// </summary>
public class ApplyPurchaseRecommendationsUseCase(
    IForecastRecommendationRepository recommendationRepository,
    ICreateUseCase<CreatePurchaseOrderDto, PurchaseOrderResponseDto> createPurchaseOrderUseCase,
    IGetByIdUseCase<PurchaseOrderResponseDto> getPurchaseOrderUseCase,
    IUnitOfWork unitOfWork
) : IApplyPurchaseRecommendationsUseCase
{
    public async Task<ApplyPurchaseResultDto> Execute(Guid userId, ApplyPurchaseRequestDto dto, CancellationToken ct)
    {
        if (dto is null)
            throw new BadRequestException("O corpo da requisição é obrigatório.");

        dto.Validate();

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BadRequestException("Informe pelo menos uma recomendação para comprar.");

        foreach (var item in dto.Items)
            item.Validate();

        var ids = dto.Items.Select(i => i.RecommendationId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new BadRequestException("A mesma recomendação foi informada mais de uma vez.");

        var today = BusinessClock.Today;
        var recommendations = (await recommendationRepository.GetAllAsync(
                r => ids.Contains(r.Id), 0, int.MaxValue, ct, query => ForecastMappings.IncludeProduct(query, today)))
            .ToDictionary(r => r.Id);

        foreach (var item in dto.Items)
        {
            if (!recommendations.TryGetValue(item.RecommendationId, out var recommendation))
                throw new NotFoundException("Recomendação não encontrada.");

            var name = recommendation.Product?.Name ?? "produto";
            if (recommendation.Status != RecommendationStatus.Pending)
                throw new BadRequestException($"A recomendação de \"{name}\" não está pendente (apenas recomendações pendentes podem ser aplicadas).");
            if (recommendation.Action != RecommendationAction.Buy)
                throw new BadRequestException($"A recomendação de \"{name}\" não é de compra.");
            if (recommendation.Product == null)
                throw new NotFoundException($"Produto da recomendação {recommendation.Id} não encontrado.");
        }

        var userNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        var groups = dto.Items
            .GroupBy(i => recommendations[i.RecommendationId].Product!.SupplierId)
            .ToList();

        var purchaseOrderIds = await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var created = new List<Guid>();
            foreach (var group in groups)
            {
                var analysisDate = group
                    .Select(i => recommendations[i.RecommendationId].ForecastRun?.CreateAt ?? recommendations[i.RecommendationId].CreateAt)
                    .Max();
                var note = $"Gerado a partir da recomendação da IA (análise de {BusinessClock.ToBusinessDate(analysisDate):dd/MM/yyyy})";
                if (userNote != null)
                    note += $" — {userNote}";

                var purchaseOrder = await createPurchaseOrderUseCase.Execute(new CreatePurchaseOrderDto
                {
                    SupplierId = group.Key,
                    Note = note,
                    Items = [.. group.Select(i => new CreatePurchaseItemDto
                    {
                        ProductId = recommendations[i.RecommendationId].ProductId,
                        Quantity = (double)i.Quantity,
                        Discount = 0,
                        CostPrice = i.CostPrice
                    })]
                }, token);

                foreach (var item in group)
                    recommendations[item.RecommendationId].MarkApplied(userId, purchaseOrderId: purchaseOrder.Id);

                created.Add(purchaseOrder.Id);
            }

            await recommendationRepository.SaveAsync(token);
            return created;
        }, ct);

        var purchaseOrders = new List<PurchaseOrderResponseDto>(purchaseOrderIds.Count);
        foreach (var id in purchaseOrderIds)
            purchaseOrders.Add(await getPurchaseOrderUseCase.Execute(id, ct));

        return new ApplyPurchaseResultDto
        {
            PurchaseOrders = purchaseOrders,
            Recommendations = [.. dto.Items.Select(i => new ForecastRecommendationDto(recommendations[i.RecommendationId]))]
        };
    }
}
