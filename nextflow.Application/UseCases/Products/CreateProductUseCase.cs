using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Application.UseCases.Base;

namespace Nextflow.Application.UseCases.Products;

public class CreateProductUseCase(
    IProductRepository repository,
    IStorageService storageService,
    ICreateCategoryProductsUseCase createCategoryProductsUseCase
    )
    : CreateUseCaseBase<Product, IProductRepository, CreateProductDto, ProductResponseDto>(repository)
{
    protected readonly IStorageService _storageService = storageService;
    protected readonly ICreateCategoryProductsUseCase _createCategoryProductsUseCase = createCategoryProductsUseCase;
    private List<CategoryResponseDto>? _lastCreatedCategories;

    protected override Product MapToEntity(CreateProductDto dto) => new(dto);

    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity, _lastCreatedCategories)
        {
            Image = _storageService.GetFileUrl(entity.Image)
        };
        _lastCreatedCategories = null;
        return dto;
    }

    protected override async Task BeforePersistence(Product entity, CreateProductDto dto, CancellationToken ct)
    {
        if (dto.Image != null)
            entity.UpdateImage(await _storageService.SaveAsync(dto.Image, ct));
    }

    protected override async Task AfterPersistence(Product entity, CreateProductDto dto, CancellationToken ct)
    {
        if (dto.CategoryIds != null && dto.CategoryIds.Count > 0)
        {
            var categories = await _createCategoryProductsUseCase.Execute(entity.Id, dto.CategoryIds, ct);
            _lastCreatedCategories = categories;
            entity.CategoryProducts = [.. categories.Select(c => new CategoryProduct(new CreateCategoryProductDto { CategoryId = c.Id, ProductId = entity.Id }))];
        }
    }
}
