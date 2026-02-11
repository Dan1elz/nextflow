
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Categories;

public class CreateCategoryProductsUseCase(ICategoryProductRepository repository, ICategoryRepository categoryRepository) : ICreateCategoryProductsUseCase
{
    private readonly ICategoryProductRepository _repository = repository;
    private readonly ICategoryRepository _categoryRepository = categoryRepository;
    public async Task<List<CategoryResponseDto>> Execute(Guid productId, List<Guid>? categoryIds, CancellationToken ct)
    {
        var categoryProducts = await _repository.GetAllAsync(c => c.ProductId == productId && c.IsActive, 0, int.MaxValue, ct);
        if (categoryProducts.Any()) await _repository.RemoveRangeAsync(categoryProducts, ct);

        if (categoryIds == null || categoryIds.Count == 0) return [];

        var categories = await _categoryRepository.GetAllAsync(c => categoryIds.Contains(c.Id) && c.IsActive, 0, int.MaxValue, ct)
            ?? throw new BadRequestException("Categorias não encontradas");

        List<CategoryProduct> newCategoryProducts = [.. categories.Select(c => new CategoryProduct(new CreateCategoryProductDto
        {
            CategoryId = c.Id,
            ProductId = productId
        }))];

        await _repository.AddRangeAsync(newCategoryProducts, ct);
        return [.. categories.Select(c => new CategoryResponseDto(c))];

    }
}
