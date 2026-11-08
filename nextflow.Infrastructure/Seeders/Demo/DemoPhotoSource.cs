using Microsoft.Extensions.Logging;

namespace Nextflow.Infrastructure.Seeders.Demo;

/// <summary>Foto baixada para o seed demo: os bytes e o tipo (<c>image/jpeg</c>, <c>image/png</c> ou <c>image/webp</c>).</summary>
public sealed record DemoPhoto(byte[] Content, string ContentType);

/// <summary>De onde o <see cref="DemoPhotosSeeder"/> baixa as fotos. Nos testes vira um fake, para o seed não depender de rede.</summary>
public interface IDemoPhotoSource
{
    /// <summary>A foto, ou <c>null</c> se não deu para baixar (sem rede, HTTP de erro, tipo não aceito). Nunca lança por rede.</summary>
    Task<DemoPhoto?> Download(string photoId, CancellationToken ct);
}

/// <summary>Baixa do Unsplash (HttpClient tipado, prazo curto): foto é enfeite e não pode segurar o startup.</summary>
public sealed class UnsplashDemoPhotoSource(HttpClient http, ILogger<UnsplashDemoPhotoSource> logger) : IDemoPhotoSource
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    private static readonly HashSet<string> AcceptedTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public async Task<DemoPhoto?> Download(string photoId, CancellationToken ct)
    {
        var url = DemoPhotos.Url(photoId);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("image/jpeg");
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Foto de demonstração {Url} respondeu {Status}.", url, (int)response.StatusCode);
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!AcceptedTypes.Contains(contentType))
            {
                logger.LogWarning("Foto de demonstração {Url} ignorada: tipo {ContentType} não aceito.", url, contentType);
                return null;
            }

            return new DemoPhoto(await response.Content.ReadAsByteArrayAsync(ct), contentType.ToLowerInvariant());
        }
        catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Não foi possível baixar a foto de demonstração {Url}.", url);
            return null;
        }
    }
}
