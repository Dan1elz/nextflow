using Nextflow.Domain.Interfaces.Utils;
using Microsoft.AspNetCore.Http;

namespace Nextflow.Utils;

public class FormFileAdapter(IFormFile file) : IFileData
{
    private readonly IFormFile _file = file;

    public string FileName => _file.FileName;
    public string ContentType => _file.ContentType;
    public long Length => _file.Length;
    public Stream OpenReadStream() => _file.OpenReadStream();
}