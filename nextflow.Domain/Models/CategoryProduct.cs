using Nextflow.Domain.Interfaces.Models;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("category_products")]
public class CategoryProduct : BaseModel, IDeletable
{
    [ForeignKey("categories"), Required(ErrorMessage = "A Categoria Ã© obrigatÃ³ria.")]
    public Guid CategoryId { get; private set; }
    public virtual Category? Category { get; set; }

    [ForeignKey("products"), Required(ErrorMessage = "O Produto Ã© obrigatÃ³rio.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; set; }

    public DateTime? UpdateAt { get; private set; }
    public bool IsActive { get; set; } = true;

    public void Update()
    {
        UpdateAt = DateTime.UtcNow;
    }
    public void Delete()
    {
        IsActive = false;
        UpdateAt = DateTime.UtcNow;
    }
    public void Reactivate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateAt = DateTime.UtcNow;
        }
    }

    public override string Preposition => "a";
    public override string Singular => "categoria do produto";
    public override string Plural => "categorias dos produtos";

    private CategoryProduct() : base() { }

    public CategoryProduct(CreateCategoryProductDto dto) : base()
    {
        CategoryId = dto.CategoryId;
        ProductId = dto.ProductId;
    }
}

