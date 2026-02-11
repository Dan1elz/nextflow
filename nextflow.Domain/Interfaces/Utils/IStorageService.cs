namespace Nextflow.Domain.Interfaces.Utils;

public interface IStorageService
{
    string BasePath { get; set; }
    Task<string> SaveAsync(IFileData file, CancellationToken ct);
    void DeleteAsync(string fileName);
    Task<byte[]> GetAsync(string fileName, CancellationToken ct);
    /// <summary>Retorna a URL pública do arquivo (ex.: local, OCI). Frontend usa esse valor direto, sem montar path.</summary>
    string? GetFileUrl(string? fileName);
}