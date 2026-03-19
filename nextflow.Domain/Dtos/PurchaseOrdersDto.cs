using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Nextflow.Domain.Attributes;
using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos;

public class CreatePurchaseOrderDto : BaseDto
{
    [NotEmptyGuid(ErrorMessage = "O Fornecedor é obrigatório.")]
    public Guid SupplierId { get; set; }

    public string? Note { get; set; }

    public List<CreatePurchaseItemDto> Items { get; set; } = [];
}

public class CreatePurchaseItemDto : BaseDto
{
    [JsonIgnore] public Guid PurchaseOrderId { get; set; } = Guid.NewGuid();

    [NotEmptyGuid(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero."), Required(ErrorMessage = "A quantidade é obrigatória.")]
    public double Quantity { get; set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O desconto não pode ser negativo.")]
    public decimal Discount { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "O preço de custo deve ser maior que zero."), Required(ErrorMessage = "O preço de custo é obrigatório.")]
    public decimal CostPrice { get; set; }
}

public class UpdatePurchaseOrderDto : BaseDto
{
    public PurchaseStatus? Status { get; set; }
    public string? Note { get; set; }
    public List<CreatePurchaseItemDto>? Items { get; set; }
}

public class PurchaseOrderResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public SupplierResponseDto? Supplier { get; set; }
    public PurchaseStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreateAt { get; set; }
    public DateTime? UpdateAt { get; set; }
    public bool Active { get; set; }
    public List<PurchaseItemResponseDto> Items { get; set; } = [];

    public PurchaseOrderResponseDto() { }

    public PurchaseOrderResponseDto(PurchaseOrder entity)
    {
        Id = entity.Id;
        SupplierId = entity.SupplierId;
        Supplier = entity.Supplier != null ? new SupplierResponseDto(entity.Supplier) : null;
        Status = entity.PurchaseStatus;
        TotalAmount = entity.TotalAmount;
        Note = entity.Note;
        CreateAt = entity.CreateAt;
        UpdateAt = entity.UpdateAt;
        Active = entity.Active;
        Items = [.. entity.PurchaseItems.Select(i => new PurchaseItemResponseDto(i))];
    }
}

public class PurchaseItemResponseDto : BaseDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public ProductResponseDto? Product { get; set; }
    public double Quantity { get; set; }
    public decimal Discount { get; set; }
    public decimal CostPrice { get; set; }
    public DateTime CreateAt { get; set; }

    public PurchaseItemResponseDto() { }

    public PurchaseItemResponseDto(PurchaseItem entity)
    {
        Id = entity.Id;
        PurchaseOrderId = entity.PurchaseOrderId;
        ProductId = entity.ProductId;
        Product = entity.Product != null ? new ProductResponseDto(entity.Product) : null;
        Quantity = entity.Quantity;
        Discount = entity.Discount;
        CostPrice = entity.CostPrice;
        CreateAt = entity.CreateAt;
    }
}
