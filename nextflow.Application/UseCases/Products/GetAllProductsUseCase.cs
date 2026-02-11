using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Products;

public class GetAllProductsUseCase(IProductRepository repository, IStorageService storageService)
    : GetAllUseCaseBase<Product, IProductRepository, ProductResponseDto>(repository)
{
    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity)
        {
            Image = storageService.GetFileUrl(entity.Image)
        };
        return dto;
    }
    protected override Func<IQueryable<Product>, IQueryable<Product>>? GetInclude() => query => query
        .Include(s => s.Supplier)
        .Include(c => c.CategoryProducts)
            .ThenInclude(cp => cp.Category);
}
