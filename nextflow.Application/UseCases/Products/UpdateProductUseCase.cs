using Nextflow.Application.UseCases.Base;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Products;

public class UpdateProductUseCase(IProductRepository repository, IStorageService storageService, ICreateCategoryProductsUseCase createCategoryProductsUseCase)
    : UpdateUseCaseBase<Product, IProductRepository, UpdateProductDto, ProductResponseDto>(repository)
{
    protected readonly IStorageService _storageService = storageService;
    protected readonly ICreateCategoryProductsUseCase _createCategoryProductsUseCase = createCategoryProductsUseCase;
    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity)
        {
            Image = _storageService.GetPublicUrl(entity.Image)
        };
        return dto;
    }
    protected override Func<IQueryable<Product>, IQueryable<Product>>? GetInclude()
    {
        var today = BusinessClock.Today;
        return query => query.IncludeCurrentPromotions(today);
    }
    protected override async Task ValidateBusinessRules(Product entity, UpdateProductDto dto, CancellationToken ct)
    {
        if (await _repository.ExistsAsync(p => p.ProductCode == dto.ProductCode && p.Id != entity.Id, ct))
            throw new BadRequestException($"Já existe um produto com o código {dto.ProductCode}.");
    }

    protected override async Task AfterPersistence(Product entity, UpdateProductDto dto, CancellationToken ct)
    {
        if (dto.CategoryIds != null && dto.CategoryIds.Count > 0)
            entity.CategoryProducts = [.. (await _createCategoryProductsUseCase.Execute(entity.Id, dto.CategoryIds, ct)).Select(c => new CategoryProduct(new CreateCategoryProductDto { CategoryId = c.Id, ProductId = entity.Id }))];
    }
}
