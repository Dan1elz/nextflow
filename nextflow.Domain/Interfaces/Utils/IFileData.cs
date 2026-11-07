namespace Nextflow.Domain.Interfaces.Utils;

public interface IFileData
{
    string FileName { get; }
    string ContentType { get; }
    /// <summary>Tamanho em bytes.</summary>
    long Length { get; }
    Stream OpenReadStream();
}
