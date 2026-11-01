namespace Nextflow.Domain.Models;

/// <summary>Vínculo categoria × produto. O produto pode estar em várias categorias, de qualquer nível (RN-CE23).</summary>
public class CategoryProduct : BaseModel
{
    public Guid CategoryId { get; private set; }
    public Guid ProductId { get; private set; }

    public virtual Category Category { get; private set; } = null!;
    public virtual Product Product { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "vínculo de categoria";
    public override string Plural => "vínculos de categoria";

    private CategoryProduct() { }

    public CategoryProduct(Guid categoryId, Guid productId)
    {
        CategoryId = categoryId;
        ProductId = productId;
    }
}
