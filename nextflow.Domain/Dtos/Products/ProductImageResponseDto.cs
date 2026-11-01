namespace Nextflow.Domain.Dtos.Products;

public class ProductImageResponseDto
{
    public string? ImageUrl { get; set; }

    public ProductImageResponseDto() { }

    public ProductImageResponseDto(string? imageUrl) => ImageUrl = imageUrl;
}
