using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>Formulário do produto (T2). A tela salva tudo de uma vez: atributos, valores e variações vêm aninhados.</summary>
public class ProductRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public ProductTypeEnum ProductType { get; set; }
    public List<Guid> CategoryIds { get; set => field = value ?? []; } = [];
    public SaleUnitEnum SaleUnit { get; set; }
    public short DecimalPlaces { get; set; }
    public bool AllowsNegative { get; set; }
    public bool TracksLots { get; set; }
    public int? ShelfLifeDays { get; set; }
    public Guid? CollectionId { get; set; }
    public DateOnly? LaunchedOn { get; set; }
    public List<ProductAttributeRequestDto> Attributes { get; set => field = value ?? []; } = [];
    public List<ProductVariantRequestDto> Variants { get; set => field = value ?? []; } = [];
    public List<Guid> EventIds { get; set => field = value ?? []; } = [];
    public List<ProductSupplierRequestDto> Suppliers { get; set => field = value ?? []; } = [];
}
