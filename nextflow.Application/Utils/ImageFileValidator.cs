using System.Diagnostics.CodeAnalysis;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Utils;

namespace Nextflow.Application.Utils;

/// <summary>
/// Regras de upload de imagem (mesmas do front: JPG, PNG ou WebP, até 5 MB).
/// Confere a extensão e a assinatura do arquivo (primeiros bytes): o Content-Type e o nome
/// vêm do cliente e não bastam para garantir que o conteúdo é mesmo uma imagem.
/// </summary>
public static class ImageFileValidator
{
    public const long MaxSizeInBytes = 5 * 1024 * 1024;

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    public static void Validate([NotNull] IFileData? file)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("A imagem é obrigatória.");

        if (file.Length > MaxSizeInBytes)
            throw new BadRequestException("O tamanho máximo da imagem é 5MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new BadRequestException("Formatos aceitos: .jpg, .jpeg, .png e .webp.");

        if (!SignatureMatches(file, extension))
            throw new BadRequestException("O conteúdo do arquivo não corresponde a uma imagem válida.");
    }

    private static bool SignatureMatches(IFileData file, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        header = header[..read];

        return extension switch
        {
            ".jpg" or ".jpeg" => header.StartsWith(Jpeg),
            ".png" => header.StartsWith(Png),
            ".webp" => header.Length == 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp),
            _ => false,
        };
    }
}
