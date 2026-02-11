using Microsoft.Extensions.Configuration;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Utils;

namespace Nextflow.Application.Utils;

public class LocalStorageService : IStorageService
{
    private readonly string _baseUrl;

    public LocalStorageService(IConfiguration configuration)
    {
        _baseUrl = configuration["Storage:BaseUrl"] ?? configuration["Urls:Application"] ?? "";
    }

    public string BasePath { get; set; } = "assets/images/products";

    public string? GetFileUrl(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (string.IsNullOrWhiteSpace(_baseUrl)) return null;
        return $"{_baseUrl.TrimEnd('/')}/assets/images/products/{fileName}";
    }

    public async Task<byte[]> GetAsync(string fileName, CancellationToken ct)
    {
        var filePath = Path.Combine(BasePath, fileName);
        if (!File.Exists(filePath)) return Array.Empty<byte>();

        return await File.ReadAllBytesAsync(filePath, ct);
    }

    public async Task<string> SaveAsync(IFileData file, CancellationToken ct)
    {
        if (file == null) throw new BadRequestException("O arquivo é obrigatório");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLower();
        if (!allowedExtensions.Contains(extension)) throw new BadRequestException("Extensão não permitida");

        if (!Directory.Exists(BasePath)) Directory.CreateDirectory(BasePath);

        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(BasePath, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        using var input = file.OpenReadStream();
        await input.CopyToAsync(stream, ct);

        return fileName;
    }

    public void DeleteAsync(string fileName)
    {
        var filePath = Path.Combine(BasePath, fileName);
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}
