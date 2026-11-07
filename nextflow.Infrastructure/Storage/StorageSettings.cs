namespace Nextflow.Infrastructure.Storage;

/// <summary>
/// Seção <c>Storage</c> da configuração (env: <c>Storage__Endpoint</c>, <c>Storage__PublicUrl</c>...).
/// </summary>
public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>Endereço que a API usa para falar com o storage (no Docker: <c>http://storage:9000</c>).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Endereço que o navegador usa (ex.: <c>http://localhost:9000</c>, na VM <c>https://nextflow-s3.zanni.tech</c>).
    /// Monta as URLs públicas e assina as URLs temporárias: a assinatura inclui o host, então precisa ser exato.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>O SDK exige uma região; o storage ignora.</summary>
    public string Region { get; set; } = "us-east-1";

    public StorageBucketNames Buckets { get; set; } = new();
}

public sealed class StorageBucketNames
{
    public string Public { get; set; } = string.Empty;
    public string Private { get; set; } = string.Empty;
}
