using Microsoft.Extensions.Logging;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Products;

public class UpdateProductImageUseCase(
    IProductRepository repository,
    IStorageService storageService,
    ILogger<UpdateProductImageUseCase> logger
) : IUpdateProductImageUseCase
{
    public async Task<ProductResponseDto> Execute(Guid productId, IFileData? image, CancellationToken ct)
    {
        ImageFileValidator.Validate(image);

        var entity = await repository.GetByIdAsync(productId, ct)
            ?? throw new NotFoundException($"Produto com id {productId} não encontrado.");

        if (!entity.IsActive)
            throw new BadRequestException("Produto está inativo e não pode ser editado.");

        var previousKey = entity.Image;
        var newKey = await storageService.UploadAsync(StorageFolder.ProductImages, image, ct);

        try
        {
            entity.UpdateImage(newKey);
            await repository.UpdateAsync(entity, ct);
        }
        catch
        {
            // O banco não gravou: a imagem nova ficaria órfã no bucket
            await storageService.DeleteAsync(StorageFolder.ProductImages.Bucket, newKey, CancellationToken.None);
            throw;
        }

        if (previousKey != null)
            await DeletePreviousImage(previousKey, productId);

        return new ProductResponseDto(entity)
        {
            Image = storageService.GetPublicUrl(entity.Image)
        };
    }

    // A troca já foi salva: falhar aqui só deixa um arquivo sobrando, não deve desfazer a operação do usuário
    private async Task DeletePreviousImage(string key, Guid productId)
    {
        try
        {
            await storageService.DeleteAsync(StorageFolder.ProductImages.Bucket, key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível apagar a imagem antiga {Key} do produto {ProductId}.", key, productId);
        }
    }
}
