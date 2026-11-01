namespace Nextflow.Domain.Dtos;

/// <summary>Arquivo recebido pela API, montado no controller: use case e validator nunca veem <c>IFormFile</c>.</summary>
public sealed record UploadFileDto(string FileName, string ContentType, long Length, Stream Content);
