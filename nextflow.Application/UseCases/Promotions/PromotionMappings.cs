using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Promotions;

internal static class PromotionMappings
{
    public static PromotionResponseDto ToResponse(Promotion entity, IStorageService storageService)
    {
        var dto = new PromotionResponseDto(entity);
        if (dto.Product != null && entity.Product != null)
            dto.Product.Image = storageService.GetPublicUrl(entity.Product.Image);
        return dto;
    }

    /// <summary>Inclui o produto (com fornecedor e promoções vigentes) para montar o <see cref="PromotionResponseDto"/>.</summary>
    public static IQueryable<Promotion> IncludeProduct(IQueryable<Promotion> query, DateOnly today) => query
        .Include(p => p.Product)
            .ThenInclude(p => p!.Supplier)
        .Include(p => p.Product)
            .ThenInclude(p => p!.Promotions.Where(pr => pr.IsActive && pr.StartDate <= today && pr.EndDate >= today));
}
