namespace Nextflow.Domain.Dtos.Products;

/// <summary>Eixo da grade (Tamanho, Cor). Sem <see cref="Id"/>, a API cria.</summary>
public class ProductAttributeRequestDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ProductAttributeValueRequestDto> Values { get; set => field = value ?? []; } = [];
}
