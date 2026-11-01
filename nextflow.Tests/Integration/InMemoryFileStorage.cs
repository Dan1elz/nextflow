using System.Collections.Concurrent;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Utils;

namespace Nextflow.Tests.Integration;

/// <summary>Storage dos testes de API: guarda em memória o que subiu e não depende do Silo nem do MinIO.</summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    public const string BaseUrl = "http://storage.test/";

    public ConcurrentDictionary<string, byte[]> Files { get; } = new();

    public async Task<string> UploadAsync(StorageFolder folder, UploadFileDto file, CancellationToken ct = default)
    {
        var key = $"{folder.Prefix}/{Guid.NewGuid()}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
        using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory, ct);
        Files[key] = memory.ToArray();
        return key;
    }

    public Task DeleteAsync(StorageBucketEnum bucket, string key, CancellationToken ct = default)
    {
        Files.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public string? GetPublicUrl(string? key) => string.IsNullOrWhiteSpace(key) ? null : BaseUrl + key;

    public string GetSignedUrl(StorageBucketEnum bucket, string key, TimeSpan expiresIn) => BaseUrl + key + "?signed";
}
