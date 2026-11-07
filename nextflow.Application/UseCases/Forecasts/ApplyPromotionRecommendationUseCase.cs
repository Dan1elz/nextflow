using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>
/// Aplica uma recomendação "Promover": cria a promoção pelo caso de uso de promoções
/// (<c>Source = AiRecommendation</c>, <c>ForecastRecommendationId</c> preenchido) e marca a recomendação como aplicada.
/// </summary>
public class ApplyPromotionRecommendationUseCase(
    IForecastRecommendationRepository recommendationRepository,
    ICreatePromotionUseCase createPromotionUseCase,
    IUnitOfWork unitOfWork
) : IApplyPromotionRecommendationUseCase
{
    private const int MaxReasonLength = 255;

    public async Task<ForecastRecommendationDto> Execute(Guid userId, Guid recommendationId, ApplyPromotionRequestDto dto, CancellationToken ct)
    {
        if (dto is null)
            throw new BadRequestException("O corpo da requisição é obrigatório.");

        dto.Validate();

        var today = BusinessClock.Today;
        var recommendation = await recommendationRepository.GetByIdAsync(recommendationId, ct, query => ForecastMappings.IncludeProduct(query, today))
            ?? throw new NotFoundException("Recomendação não encontrada.");

        if (recommendation.Status != RecommendationStatus.Pending)
            throw new BadRequestException("Apenas recomendações pendentes podem ser aplicadas.");
        if (recommendation.Action != RecommendationAction.Promote)
            throw new BadRequestException("Esta recomendação não é de promoção.");

        var analysisDate = BusinessClock.ToBusinessDate(recommendation.ForecastRun?.CreateAt ?? recommendation.CreateAt);
        var reason = string.IsNullOrWhiteSpace(dto.Reason)
            ? $"Recomendação da IA (análise de {analysisDate:dd/MM/yyyy})"
            : dto.Reason.Trim();
        if (reason.Length > MaxReasonLength)
            reason = reason[..MaxReasonLength];

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var promotion = await createPromotionUseCase.Execute(new CreatePromotionDto
            {
                ProductId = recommendation.ProductId,
                DiscountPercent = dto.DiscountPercent,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                Reason = reason,
                Source = PromotionSource.AiRecommendation,
                ForecastRecommendationId = recommendation.Id,
                CreatedByUserId = userId
            }, token);

            recommendation.MarkApplied(userId, promotionId: promotion.Id);
            await recommendationRepository.SaveAsync(token);
            return promotion.Id;
        }, ct);

        // Recarrega para refletir o preço efetivo (a promoção pode já estar vigente hoje)
        var updated = await recommendationRepository.GetByIdAsync(recommendationId, ct, query => ForecastMappings.IncludeProduct(query, today));
        return new ForecastRecommendationDto(updated ?? recommendation);
    }
}
