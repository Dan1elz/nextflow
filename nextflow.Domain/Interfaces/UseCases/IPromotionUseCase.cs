using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.UseCases.Base;

namespace Nextflow.Domain.Interfaces.UseCases;

/// <summary>
/// Criação de promoções com todas as regras (datas, sobreposição, produto ativo).
/// Reutilizado pelo módulo de previsão para criar promoções a partir de recomendações
/// (<c>Source = AiRecommendation</c> + <c>ForecastRecommendationId</c>).
/// </summary>
public interface ICreatePromotionUseCase : ICreateUseCase<CreatePromotionDto, PromotionResponseDto>
{
}
