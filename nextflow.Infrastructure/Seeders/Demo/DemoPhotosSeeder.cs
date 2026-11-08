using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Fotos dos produtos de demonstração (só com <c>Seed:Demo:Enabled</c>): baixa do Unsplash (<see cref="DemoPhotos"/>) e sobe
/// no storage S3 (Silo) na pasta de imagem de produto, como o upload da tela.
/// <para>
/// Idempotente: só mexe em produto do catálogo demo que está sem imagem, então um restart completa o que faltou (sem rede na
/// primeira subida, por exemplo) e não duplica. Foto é enfeite: a que não baixa fica de fora (LogWarning) e o startup segue.
/// Se o banco não gravar, apaga do bucket o que subiu, como o <c>UploadProductImageUseCase</c>.
/// </para>
/// </summary>
public class DemoPhotosSeeder(
    IConfiguration configuration,
    IProductRepository productRepository,
    IProductVariantRepository variantRepository,
    IFileStorage fileStorage,
    IDemoPhotoSource photoSource,
    IUnitOfWork unitOfWork,
    ILogger<DemoPhotosSeeder> logger) : ISeeder
{
    private const int MaxParallelDownloads = 6;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    public int Order => 80;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!bool.TryParse(configuration[DemoCatalogSeeder.EnabledKey], out var enabled) || !enabled)
            return;

        // O produto do catálogo é achado pelo SKU da primeira variação (o código, ou código-sufixo na grade).
        var photoBySku = DemoCatalog.Products
            .Where(p => DemoPhotos.ByProductCode.ContainsKey(p.Code))
            .ToDictionary(p => DemoCatalog.VariantsOf(p)[0].Sku, p => (p.Code, PhotoId: DemoPhotos.ByProductCode[p.Code]));
        var skus = photoBySku.Keys.ToList();
        var variants = await variantRepository.GetAll(v => skus.Contains(v.Sku), 0, int.MaxValue, ct: ct);
        var photoByProduct = variants.ToDictionary(v => v.ProductId, v => photoBySku[v.Sku]);
        if (photoByProduct.Count == 0)
            return;

        var productIds = photoByProduct.Keys.ToList();
        var products = await productRepository.GetAllForUpdate(p => productIds.Contains(p.Id) && p.ImageKey == null, ct);
        if (products.Count == 0)
            return;

        using var throttle = new SemaphoreSlim(MaxParallelDownloads);
        var results = await Task.WhenAll(products.Select(async product =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var (code, photoId) = photoByProduct[product.Id];
                return (Product: product, Key: await Upload(code, photoId, ct));
            }
            finally
            {
                throttle.Release();
            }
        }));

        var uploaded = results.Where(r => r.Key is not null).ToList();
        if (uploaded.Count == 0)
        {
            logger.LogWarning("Fotos de demonstração: nenhuma das {Total} baixou; os produtos seguem sem imagem.", products.Count);
            return;
        }

        foreach (var (product, key) in uploaded)
            product.SetImage(key!);

        try
        {
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            foreach (var (_, key) in uploaded)
                await fileStorage.DeleteAsync(StorageFolder.ProductImages.Bucket, key!, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Fotos de demonstração: {Uploaded} de {Total} produtos com imagem.", uploaded.Count, products.Count);
    }

    private async Task<string?> Upload(string code, string photoId, CancellationToken ct)
    {
        var photo = await photoSource.Download(photoId, ct);
        if (photo is null || !Extensions.TryGetValue(photo.ContentType, out var extension))
            return null;

        try
        {
            using var content = new MemoryStream(photo.Content, writable: false);
            var file = new UploadFileDto($"{code}{extension}", photo.ContentType, photo.Content.LongLength, content);
            return await fileStorage.UploadAsync(StorageFolder.ProductImages, file, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Storage fora do ar não derruba o seed: a foto fica para o próximo startup.
            logger.LogWarning(ex, "Não foi possível subir a foto de demonstração do produto {Code} no storage.", code);
            return null;
        }
    }
}
