using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Products;

public class RemoveProductImageUseCase(
    IProductRepository repository,
    IStorageService storageService,
    ILogger<RemoveProductImageUseCase> logger
) : IRemoveProductImageUseCase
{
    public async Task<ProductResponseDto> Execute(Guid productId, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException($"Produto com id {productId} não encontrado.");

        if (!entity.IsActive)
            throw new BadRequestException("Produto está inativo e não pode ser editado.");

        if (entity.Image != null)
        {
            var key = entity.Image;

            // Banco primeiro: se falhar, o produto continua apontando para um arquivo que ainda existe
            entity.RemoveImage();
            await repository.UpdateAsync(entity, ct);

            try
            {
                await storageService.DeleteAsync(StorageFolder.ProductImages.Bucket, key, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Não foi possível apagar a imagem {Key} do produto {ProductId}.", key, productId);
            }
        }

        return new ProductResponseDto(entity)
        {
            Image = storageService.GetPublicUrl(entity.Image)
        };
    }
}
