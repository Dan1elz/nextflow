using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Promotions;

public class GetAllPromotionsUseCase(IPromotionRepository repository, IStorageService storageService)
    : GetAllUseCaseBase<Promotion, IPromotionRepository, PromotionResponseDto>(repository)
{
    private readonly IStorageService _storageService = storageService;

    protected override PromotionResponseDto MapToResponseDto(Promotion entity)
        => PromotionMappings.ToResponse(entity, _storageService);

    protected override Func<IQueryable<Promotion>, IQueryable<Promotion>>? GetInclude()
    {
        var today = BusinessClock.Today;
        return query => PromotionMappings.IncludeProduct(query, today);
    }

    protected override void ApplyFilters(FilterExpressionBuilder<Promotion> builder, FilterSet filters)
    {
        builder.WhereGuidEquals(filters, "productId", p => p.ProductId);

        // status = active | scheduled | ended | canceled (calculado com a data de negócio)
        if (filters.TryGetString("status", out var status))
        {
            var today = BusinessClock.Today;
            switch (status.Trim().ToLowerInvariant())
            {
                case Promotion.StatusActive:
                    builder.And(p => p.IsActive && p.StartDate <= today && p.EndDate >= today);
                    break;
                case Promotion.StatusScheduled:
                    builder.And(p => p.IsActive && p.StartDate > today);
                    break;
                case Promotion.StatusEnded:
                    builder.And(p => p.IsActive && p.EndDate < today);
                    break;
                case Promotion.StatusCanceled:
                    builder.And(p => !p.IsActive);
                    break;
            }
        }

        // source = 1 | 2 | Manual | AiRecommendation
        if (filters.TryGetString("source", out var sourceRaw))
        {
            PromotionSource? source = null;
            if (FilterValueParsers.TryParseInt(sourceRaw, out var sourceInt) && Enum.IsDefined(typeof(PromotionSource), (byte)sourceInt))
                source = (PromotionSource)sourceInt;
            else if (Enum.TryParse<PromotionSource>(sourceRaw, true, out var parsed) && Enum.IsDefined(parsed))
                source = parsed;

            if (source.HasValue)
            {
                var value = source.Value;
                builder.And(p => p.Source == value);
            }
        }
    }
}
