using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Products;

public class UpdateProductImageUseCase(
    IProductRepository repository,
    IStorageService storageService
) : IUpdateProductImageUseCase
{
    public async Task<ProductResponseDto> Execute(Guid productId, IFileData image, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException($"Produto com id {productId} não encontrado.");

        if (!entity.IsActive)
            throw new BadRequestException("Produto está inativo e não pode ser editado.");

        var fileName = await storageService.SaveAsync(image, ct);
        entity.UpdateImage(fileName);
        await repository.UpdateAsync(entity, ct);

        var dto = new ProductResponseDto(entity)
        {
            Image = storageService.GetFileUrl(entity.Image)
        };
        return dto;
    }
}
