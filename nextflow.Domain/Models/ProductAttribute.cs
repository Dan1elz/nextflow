using Nextflow.Domain.Dtos.Products;

namespace Nextflow.Domain.Models;

/// <summary>Eixo da grade de um produto (Tamanho, Cor). Criado e sincronizado só pelo <see cref="Product"/>.</summary>
public class ProductAttribute : BaseModel
{
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public short SortOrder { get; private set; }

    public virtual Product Product { get; private set; } = null!;
    public virtual ICollection<ProductAttributeValue> Values { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "atributo";
    public override string Plural => "atributos";

    private ProductAttribute() { }

    public ProductAttribute(ProductAttributeRequestDto dto, Guid productId, short sortOrder)
    {
        ProductId = productId;
        Update(dto, sortOrder);
    }

    public void Update(ProductAttributeRequestDto dto, short sortOrder)
    {
        Name = dto.Name.Trim();
        SortOrder = sortOrder;
    }

    /// <summary>Adiciona, atualiza por id e apaga o que saiu; registra em <paramref name="valueIds"/> o id de cada chave.</summary>
    public void SyncValues(List<ProductAttributeValueRequestDto> dtos, Dictionary<string, Guid> valueIds)
    {
        var kept = dtos.Select(dto => dto.Id).ToHashSet();
        foreach (var value in Values.Where(value => !kept.Contains(value.Id)).ToList())
            Values.Remove(value);

        for (var index = 0; index < dtos.Count; index++)
        {
            var dto = dtos[index];
            var value = Values.FirstOrDefault(v => v.Id == dto.Id);
            if (value is null)
            {
                value = new ProductAttributeValue(dto, Id, (short)index);
                Values.Add(value);
            }
            else
            {
                value.Update(dto, (short)index);
            }

            valueIds[dto.GetKey()!] = value.Id;
        }
    }
}
