using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Products;

public class GetProductByIdUseCase(IProductRepository repository, IStorageService storageService)
    : GetByIdUseCaseBase<Product, IProductRepository, ProductResponseDto>(repository)
{
    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity)
        {
            Image = storageService.GetPublicUrl(entity.Image)
        };
        return dto;
    }
    protected override Func<IQueryable<Product>, IQueryable<Product>>? GetInclude()
    {
        var today = BusinessClock.Today;
        return query => query
            .Include(s => s.Supplier)
            .Include(c => c.CategoryProducts)
                .ThenInclude(cp => cp.Category)
            .IncludeCurrentPromotions(today);
    }
}
