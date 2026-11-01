namespace Nextflow.Domain.Models;

/// <summary>Valor que compõe a combinação de uma variação: "P / Azul" são duas linhas.</summary>
public class ProductVariantValue : BaseModel
{
    public Guid VariantId { get; private set; }
    public Guid AttributeValueId { get; private set; }

    public virtual ProductVariant Variant { get; private set; } = null!;
    public virtual ProductAttributeValue AttributeValue { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "valor da variação";
    public override string Plural => "valores da variação";

    private ProductVariantValue() { }

    public ProductVariantValue(Guid variantId, Guid attributeValueId)
    {
        VariantId = variantId;
        AttributeValueId = attributeValueId;
    }
}
