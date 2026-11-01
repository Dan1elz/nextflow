using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Utils;

namespace Nextflow.Domain.Interfaces.Infrastructure.Services;

/// <summary>
/// Storage de arquivos compatível com S3 (ADR 0006). O banco guarda só a chave devolvida pelo upload
/// (<c>products/3f2a.webp</c>), nunca a URL: a URL é montada na leitura. Validar o arquivo é de quem chama.
/// </summary>
public interface IFileStorage
{
    /// <summary>Grava o arquivo em <paramref name="folder"/> com nome único e devolve a chave.</summary>
    Task<string> UploadAsync(StorageFolder folder, UploadFileDto file, CancellationToken ct = default);

    /// <summary>Apaga o arquivo. Não falha se ele já não existir.</summary>
    Task DeleteAsync(StorageBucketEnum bucket, string key, CancellationToken ct = default);

    /// <summary>URL pública de um arquivo do bucket público; <c>null</c> se não houver chave.</summary>
    string? GetPublicUrl(string? key);

    /// <summary>URL temporária para ler um arquivo de qualquer bucket.</summary>
    string GetSignedUrl(StorageBucketEnum bucket, string key, TimeSpan expiresIn);
}
