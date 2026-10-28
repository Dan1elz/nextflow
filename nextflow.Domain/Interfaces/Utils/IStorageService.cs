using Nextflow.Domain.Enums;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Interfaces.Utils;

/// <summary>
/// Storage de arquivos (S3). O banco guarda só a chave devolvida pelo upload
/// (ex.: <c>products/3f2a….webp</c>), nunca a URL: a URL é montada na leitura.
/// Validar tipo e tamanho do arquivo é responsabilidade de quem chama (use case).
/// </summary>
public interface IStorageService
{
    /// <summary>Grava o arquivo em <paramref name="folder"/> com nome único e devolve a chave.</summary>
    Task<string> UploadAsync(StorageFolder folder, IFileData file, CancellationToken ct);

    Task<bool> ExistsAsync(StorageBucket bucket, string key, CancellationToken ct);

    /// <summary>Apaga o arquivo. Não falha se ele já não existir.</summary>
    Task DeleteAsync(StorageBucket bucket, string key, CancellationToken ct);

    /// <summary>URL pública de um arquivo do bucket <see cref="StorageBucket.Public"/>; <c>null</c> se não houver chave.</summary>
    string? GetPublicUrl(string? key);

    /// <summary>URL temporária para ler um arquivo de qualquer bucket (uso típico: <see cref="StorageBucket.Private"/>).</summary>
    string GetSignedUrl(StorageBucket bucket, string key, TimeSpan expiresIn);
}
