using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>Descarta uma recomendação pendente (motivo opcional, até 255 caracteres).</summary>
public class DismissRecommendationUseCase(IForecastRecommendationRepository recommendationRepository) : IDismissRecommendationUseCase
{
    public async Task<ForecastRecommendationDto> Execute(Guid recommendationId, string? reason, CancellationToken ct)
    {
        var today = BusinessClock.Today;
        var recommendation = await recommendationRepository.GetByIdAsync(recommendationId, ct, query => ForecastMappings.IncludeProduct(query, today))
            ?? throw new NotFoundException("Recomendação não encontrada.");

        recommendation.Dismiss(reason); // só Pending (senão 400)
        await recommendationRepository.SaveAsync(ct);

        return new ForecastRecommendationDto(recommendation);
    }
}
