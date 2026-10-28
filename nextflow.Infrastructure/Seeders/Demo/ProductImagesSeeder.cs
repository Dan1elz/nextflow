using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Utils;
using Nextflow.Infrastructure.Database;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>
/// Fotos dos produtos de demonstração: baixa do Unsplash (licença livre) e sobe no storage.
/// Só mexe em produto do catálogo de demonstração que está sem imagem, então roda de novo sem duplicar
/// e completa um banco que já tinha os produtos. Foto é enfeite: sem internet, o produto fica sem foto
/// (LogWarning) e o startup segue.
/// </summary>
public class ProductImagesSeeder(
    AppDbContext context,
    IStorageService storage,
    IHttpClientFactory httpClientFactory,
    IOptions<DemoSeedSettings> options,
    ILogger<ProductImagesSeeder> logger) : ISeeder
{
    private const int MaxParallelDownloads = 6;
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(20);
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    public int Order => 80;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled)
            return;

        var photoByCode = DemoStoreCatalog.Products.ToDictionary(p => p.Code, p => p.PhotoId);
        var codes = photoByCode.Keys.ToList();
        var products = await context.Products
            .Where(p => codes.Contains(p.ProductCode) && p.Image == null)
            .ToListAsync(ct);

        if (products.Count == 0)
            return;

        using var http = httpClientFactory.CreateClient();
        http.Timeout = DownloadTimeout;
        using var throttle = new SemaphoreSlim(MaxParallelDownloads);

        var uploads = await Task.WhenAll(products.Select(async product =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                return (product, Key: await UploadPhotoAsync(http, product.ProductCode, photoByCode[product.ProductCode], ct));
            }
            finally
            {
                throttle.Release();
            }
        }));

        var uploaded = uploads.Where(x => x.Key != null).ToList();
        foreach (var (product, key) in uploaded)
            product.UpdateImage(key);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch
        {
            // O banco não gravou: apaga as fotos para não deixar arquivos órfãos no bucket
            foreach (var (_, key) in uploaded)
                await storage.DeleteAsync(StorageFolder.ProductImages.Bucket, key!, CancellationToken.None);
            throw;
        }

        logger.LogInformation("Fotos de demonstração: {Uploaded} de {Total} produtos com imagem.", uploaded.Count, products.Count);
    }

    private async Task<string?> UploadPhotoAsync(HttpClient http, string productCode, string photoId, CancellationToken ct)
    {
        var url = $"https://images.unsplash.com/photo-{photoId}?w=800&auto=format&fit=crop&q=80";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("image/jpeg");
            using var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!Extensions.TryGetValue(contentType, out var extension))
            {
                logger.LogWarning("Foto do produto {ProductCode} ignorada: tipo {ContentType} não aceito.", productCode, contentType);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(ct);
            return await storage.UploadAsync(StorageFolder.ProductImages, new InMemoryFileData($"{productCode}{extension}", contentType, content), ct);
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Não foi possível baixar a foto do produto {ProductCode} ({Url}).", productCode, url);
            return null;
        }
    }

    private sealed class InMemoryFileData(string fileName, string contentType, byte[] content) : IFileData
    {
        public string FileName => fileName;
        public string ContentType => contentType;
        public long Length => content.LongLength;
        public Stream OpenReadStream() => new MemoryStream(content, writable: false);
    }
}
