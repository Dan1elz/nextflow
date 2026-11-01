using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Models;

/// <summary>Valor de um eixo da grade (P, Azul). A ordem do formulário é a ordem da grade.</summary>
public class ProductAttributeValue : BaseModel
{
    public Guid AttributeId { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public short SortOrder { get; private set; }

    public virtual ProductAttribute Attribute { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "valor de atributo";
    public override string Plural => "valores de atributo";

    private ProductAttributeValue() { }

    public ProductAttributeValue(ProductAttributeValueRequestDto dto, Guid attributeId, short sortOrder)
    {
        AttributeId = attributeId;
        Update(dto, sortOrder);
    }

    public void Update(ProductAttributeValueRequestDto dto, short sortOrder)
    {
        Value = dto.Value.Trim();
        SortOrder = sortOrder;
    }
}
