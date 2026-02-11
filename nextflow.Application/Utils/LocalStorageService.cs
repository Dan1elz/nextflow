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

    /// <summary>
    /// Retorna URL da imagem: absoluta se Storage:BaseUrl estiver configurado, caso contrário path relativo (ex.: /assets/images/products/xxx.jpg).
    /// O frontend deve concatenar a URL da API quando o retorno for path relativo.
    /// </summary>
    public string? GetFileUrl(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var path = $"/assets/images/products/{fileName}";
        if (string.IsNullOrWhiteSpace(_baseUrl)) return path;
        return $"{_baseUrl.TrimEnd('/')}{path}";
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
