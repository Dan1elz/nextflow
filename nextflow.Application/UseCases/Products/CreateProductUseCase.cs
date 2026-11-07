using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Application.UseCases.Base;
using Nextflow.Application.Utils;

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
    private string? _uploadedImageKey;

    public override async Task<ProductResponseDto> Execute(CreateProductDto dto, CancellationToken ct)
    {
        try
        {
            return await base.Execute(dto, ct);
        }
        catch when (_uploadedImageKey != null)
        {
            // A imagem subiu mas o produto não foi gravado: apaga para não deixar arquivo órfão
            await _storageService.DeleteAsync(StorageFolder.ProductImages.Bucket, _uploadedImageKey, CancellationToken.None);
            throw;
        }
        finally
        {
            _uploadedImageKey = null;
        }
    }

    protected override Product MapToEntity(CreateProductDto dto) => new(dto);

    protected override ProductResponseDto MapToResponseDto(Product entity)
    {
        var dto = new ProductResponseDto(entity, _lastCreatedCategories)
        {
            Image = _storageService.GetPublicUrl(entity.Image)
        };
        _lastCreatedCategories = null;
        return dto;
    }

    protected override async Task ValidateBusinessRules(CreateProductDto dto, CancellationToken ct)
    {
        if (await _repository.ExistsAsync(p => p.ProductCode == dto.ProductCode, ct))
            throw new BadRequestException($"Já existe um produto com o código {dto.ProductCode}.");

        if (dto.Image != null)
            ImageFileValidator.Validate(dto.Image);
    }

    protected override async Task BeforePersistence(Product entity, CreateProductDto dto, CancellationToken ct)
    {
        if (dto.Image == null) return;

        _uploadedImageKey = await _storageService.UploadAsync(StorageFolder.ProductImages, dto.Image, ct);
        entity.UpdateImage(_uploadedImageKey);
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
