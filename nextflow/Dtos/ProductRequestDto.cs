namespace Nextflow.Dtos;

/// <summary>
/// DTO para criação de produto (POST) com multipart/form-data. Herda de UpdateProductRequestDto e adiciona Image.
/// </summary>
public class ProductRequestDto : UpdateProductRequestDto
{
    public IFormFile? Image { get; set; }
}
