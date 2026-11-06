using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Products;

/// <summary>O que o formulário T2 precisa, de uma vez: os campos do <see cref="ProductRequestDto"/>, a imagem e as opções.</summary>
public class ProductFormDto : ProductRequestDto
{
    public Guid? Id { get; set; }
    public string? ImageUrl { get; set; }
    public ProductFormOptionsDto Options { get; set; } = new();

    public ProductFormDto() { }

    public ProductFormDto(ProductFormOptionsDto options) => Options = options;

    public ProductFormDto(Product entity, string? imageUrl, ProductFormOptionsDto options)
    {
        Id = entity.Id;
        ImageUrl = imageUrl;
        Options = options;
        Name = entity.Name;
        Brand = entity.Brand;
        Description = entity.Description;
        ProductType = entity.ProductType;
        CategoryIds = [.. entity.Categories.Select(link => link.CategoryId)];
        SaleUnit = entity.SaleUnit;
        DecimalPlaces = entity.DecimalPlaces;
        AllowsNegative = entity.AllowsNegative;
        TracksLots = entity.TracksLots;
        ShelfLifeDays = entity.ShelfLifeDays;
        CollectionId = entity.CollectionId;
        LaunchedOn = entity.LaunchedOn;
        Attributes = [.. entity.Attributes.OrderBy(a => a.SortOrder).Select(attribute => new ProductAttributeRequestDto
        {
            Id = attribute.Id,
            Name = attribute.Name,
            Values = [.. attribute.Values.OrderBy(v => v.SortOrder).Select(value => new ProductAttributeValueRequestDto
            {
                Id = value.Id,
                Value = value.Value,
            })],
        })];
        DefaultMinQuantity = entity.DefaultMinQuantity;
        TargetCoverageDays = entity.TargetCoverageDays;
        var variants = entity.Variants.Where(v => v.RemovedOn == null).OrderBy(v => v.SortOrder).ToList();
        LocationMinimums = [.. variants.SelectMany((variant, index) => variant.LocationMinimums
            .OrderBy(m => m.CreatedAt)
            .Select(minimum => new ProductLocationMinimumDto
            {
                VariantIndex = index,
                LocationId = minimum.LocationId,
                MinQuantity = minimum.MinQuantity,
            }))];
        Variants = [.. variants.Select(variant => new ProductVariantRequestDto
        {
            Id = variant.Id,
            AttributeValueIds = [.. variant.AttributeValues.Select(link => link.AttributeValueId.ToString())],
            Sku = variant.Sku,
            Barcode = variant.Barcode,
            Price = variant.Price,
            IsActive = variant.IsActive,
        })];
        EventIds = [.. entity.Events.Select(link => link.EventId)];
        Suppliers = [.. entity.Suppliers.OrderByDescending(s => s.IsPreferred).ThenBy(s => s.CreatedAt).Select(link => new ProductSupplierRequestDto
        {
            SupplierId = link.SupplierId,
            IsPreferred = link.IsPreferred,
            SupplierSku = link.SupplierSku,
            LeadTimeDays = link.LeadTimeDays,
            MinOrderQty = link.MinOrderQty,
            OrderMultiple = link.OrderMultiple,
        })];
    }
}
