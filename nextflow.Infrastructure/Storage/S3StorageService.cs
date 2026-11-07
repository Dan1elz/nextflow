using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Utils;

namespace Nextflow.Infrastructure.Storage;

/// <summary>
/// <see cref="IStorageService"/> sobre qualquer storage compatível com S3 (no projeto: Silo).
/// Usa dois clientes: um fala com o storage pela rede interna e o outro só assina URLs
/// com o endereço público, porque a assinatura inclui o host que o navegador vai chamar.
/// Singleton: o <see cref="AmazonS3Client"/> é thread-safe e reaproveita conexões.
/// </summary>
public sealed class S3StorageService : IStorageService, IDisposable
{
    // O Content-Type gravado vem da extensão (já validada pelo use case), não do que o cliente declarou
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
    };

    private readonly AmazonS3Client _client;
    private readonly AmazonS3Client _signer;
    private readonly Uri _publicUrl;
    private readonly string _publicBaseUrl;
    private readonly StorageBucketNames _buckets;

    public S3StorageService(IOptions<StorageSettings> options)
    {
        var settings = options.Value;
        Validate(settings);

        var credentials = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        _publicBaseUrl = settings.PublicUrl.TrimEnd('/');
        _publicUrl = new Uri(_publicBaseUrl);
        _buckets = settings.Buckets;
        _client = new AmazonS3Client(credentials, CreateConfig(settings.Endpoint, settings.Region));
        _signer = new AmazonS3Client(credentials, CreateConfig(settings.PublicUrl, settings.Region));
    }

    public async Task<string> UploadAsync(StorageFolder folder, IFileData file, CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var key = $"{folder.Prefix}/{Guid.NewGuid()}{extension}";

        await using var stream = file.OpenReadStream();
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = BucketName(folder.Bucket),
            Key = key,
            InputStream = stream,
            ContentType = ContentTypes.GetValueOrDefault(extension, "application/octet-stream"),
        }, ct);

        return key;
    }

    public async Task<bool> ExistsAsync(StorageBucket bucket, string key, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(BucketName(bucket), key, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(StorageBucket bucket, string key, CancellationToken ct)
        => _client.DeleteObjectAsync(BucketName(bucket), key, ct);

    public string? GetPublicUrl(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return $"{_publicBaseUrl}/{_buckets.Public}/{key}";
    }

    public string GetSignedUrl(StorageBucket bucket, string key, TimeSpan expiresIn)
        => _signer.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName(bucket),
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiresIn),
            // O padrão do SDK é HTTPS; no dev o endereço público é http://localhost:9000
            Protocol = _publicUrl.Scheme == Uri.UriSchemeHttp ? Protocol.HTTP : Protocol.HTTPS,
        });

    public void Dispose()
    {
        _client.Dispose();
        _signer.Dispose();
    }

    private string BucketName(StorageBucket bucket) => bucket switch
    {
        StorageBucket.Public => _buckets.Public,
        StorageBucket.Private => _buckets.Private,
        _ => throw new ArgumentOutOfRangeException(nameof(bucket), bucket, "Bucket desconhecido."),
    };

    private static AmazonS3Config CreateConfig(string serviceUrl, string region) => new()
    {
        ServiceURL = serviceUrl,
        AuthenticationRegion = region,
        // Storage próprio responde em http://host:porta/<bucket>/<chave>, não em <bucket>.host
        ForcePathStyle = true,
        // O SDK v4 manda checksums novos em toda requisição; só quando a operação exigir mantém compatível
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
    };

    // Falha no startup com mensagem clara em vez de erro obscuro no primeiro upload
    private static void Validate(StorageSettings settings)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(settings.Endpoint)) missing.Add("Storage:Endpoint");
        if (string.IsNullOrWhiteSpace(settings.PublicUrl)) missing.Add("Storage:PublicUrl");
        if (string.IsNullOrWhiteSpace(settings.AccessKey)) missing.Add("Storage:AccessKey");
        if (string.IsNullOrWhiteSpace(settings.SecretKey)) missing.Add("Storage:SecretKey");
        if (string.IsNullOrWhiteSpace(settings.Buckets.Public)) missing.Add("Storage:Buckets:Public");
        if (string.IsNullOrWhiteSpace(settings.Buckets.Private)) missing.Add("Storage:Buckets:Private");

        if (missing.Count > 0)
            throw new InvalidOperationException($"Configuração do storage incompleta: {string.Join(", ", missing)}.");
    }
}
