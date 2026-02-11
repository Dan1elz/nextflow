using System.ComponentModel.DataAnnotations;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos;

public class CreateProductDto : BaseDto
{
    [NotEmptyGuid(ErrorMessage = "O Fornecedor é obrigatório.")]
    public Guid SupplierId { get; set; }

    [Required(ErrorMessage = "O código é obrigatório")]
    public string ProductCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "O nome é obrigatório"), StringLength(100, ErrorMessage = "O nome não pode exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A descrição é obrigatória"), StringLength(500, ErrorMessage = "A descrição não pode exceder 500 caracteres")]
    public string Description { get; set; } = string.Empty;
    public IFileData? Image { get; set; }
    public List<Guid>? CategoryIds { get; set; }

    [Required(ErrorMessage = "A quantidade em estoque é obrigatória"), Range(0, double.MaxValue, ErrorMessage = "A quantidade em estoque não pode ser negativa")]
    public decimal Quantity { get; set; }

    [Required(ErrorMessage = "O tipo de unidade é obrigatório")]
    public UnitType UnitType { get; set; }

    [Required(ErrorMessage = "O preço é obrigatório"), Range(0.0, double.MaxValue, ErrorMessage = "O preço não pode ser negativo")]
    public decimal Price { get; set; }
    public DateOnly? Validity { get; set; }
}
public class UpdateProductDto : CreateProductDto { }

public class ProductResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierResponseDto? Supplier { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Image { get; set; }
    public decimal Quantity { get; set; }
    public UnitType UnitType { get; set; }
    public decimal Price { get; set; }
    public DateOnly? Validity { get; set; }
    public List<CategoryResponseDto>? Categories { get; set; }

    public ProductResponseDto() { }

    public ProductResponseDto(Product entity) : this(entity, null) { }

    /// <summary>
    /// Construtor com categorias opcionais (ex.: após criar produto, quando CategoryProducts ainda não têm Category carregada).
    /// </summary>
    public ProductResponseDto(Product entity, List<CategoryResponseDto>? categoriesOverride)
    {
        Id = entity.Id;
        SupplierId = entity.SupplierId;
        Supplier = entity.Supplier != null ? new SupplierResponseDto(entity.Supplier) : null;
        ProductCode = entity.ProductCode;
        Name = entity.Name;
        Description = entity.Description;
        Quantity = entity.Quantity;
        UnitType = entity.UnitType;
        Price = entity.Price;
        Validity = entity.Validity;
        Image = entity.Image;
        Categories = categoriesOverride ?? [.. entity.CategoryProducts
            .Where(cp => cp.Category != null)
            .Select(cp => new CategoryResponseDto(cp.Category!))];
    }
}