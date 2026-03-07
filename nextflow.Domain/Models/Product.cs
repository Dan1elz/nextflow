using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Models;

[Table("products")]
public class Product : BaseModel, IUpdatable<UpdateProductDto>, IDeletable
{
    [ForeignKey("suppliers"), Required(ErrorMessage = "O Fornecedor Ã© obrigatÃ³rio.")]
    public Guid SupplierId { get; private set; }
    public virtual Supplier? Supplier { get; set; }

    [Required(ErrorMessage = "O cÃ³digo Ã© obrigatÃ³rio")]
    public string ProductCode { get; private set; } = string.Empty;

    [Required(ErrorMessage = "O nome Ã© obrigatÃ³rio"), StringLength(100, ErrorMessage = "O nome nÃ£o pode exceder 100 caracteres")]
    public string Name { get; private set; } = string.Empty;

    [Required(ErrorMessage = "A descriÃ§Ã£o Ã© obrigatÃ³ria"), StringLength(500, ErrorMessage = "A descriÃ§Ã£o nÃ£o pode exceder 500 caracteres")]
    public string Description { get; private set; } = string.Empty;

    [StringLength(255, ErrorMessage = "O caminho da imagem nÃ£o pode exceder 255 caracteres")]
    public string? Image { get; private set; } = string.Empty;

    [Required(ErrorMessage = "A quantidade em estoque Ã© obrigatÃ³ria"), Range(0, double.MaxValue, ErrorMessage = "A quantidade em estoque nÃ£o pode ser negativa")]
    public decimal Quantity { get; private set; }

    [Required(ErrorMessage = "O tipo de unidade Ã© obrigatÃ³rio")]
    public UnitType UnitType { get; private set; }

    [Required(ErrorMessage = "O preÃ§o Ã© obrigatÃ³rio"), Range(0.0, double.MaxValue, ErrorMessage = "O preÃ§o nÃ£o pode ser negativo")]
    public decimal Price { get; private set; }
    public DateOnly? Validity { get; private set; }
    public virtual ICollection<StockMovement> StockMovements { get; set; } = [];
    public virtual ICollection<CategoryProduct> CategoryProducts { get; set; } = [];
    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];

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

    public override string Preposition => "o";
    public override string Singular => "produto";
    public override string Plural => "produtos";

    private Product() : base() { }

    public Product(CreateProductDto dto) : base()
    {
        SupplierId = dto.SupplierId;
        ProductCode = dto.ProductCode;
        Name = dto.Name;
        Description = dto.Description;
        Quantity = dto.Quantity;
        UnitType = dto.UnitType;
        Price = dto.Price;
        Validity = dto.Validity;
    }
    public void Update(UpdateProductDto dto)
    {
        SupplierId = dto.SupplierId;
        ProductCode = dto.ProductCode;
        Name = dto.Name;
        Description = dto.Description;
        Quantity = dto.Quantity;
        UnitType = dto.UnitType;
        Price = dto.Price;
        Validity = dto.Validity;
        Update();
    }

    public void UpdateImage(string? imagePath)
    {
        Image = imagePath;
        Update();
    }
    public void RemoveImage()
    {
        Image = null;
        Update();
    }

    public void SetMovementStock(StockMovementDto dto)
    {
        var quantity = (decimal)dto.Quantity;
        if (quantity <= 0)
            throw new BadRequestException("A quantidade movimentada deve ser maior que zero.");

        switch (dto.MovementType)
        {
            case MovementType.Entry:
            case MovementType.Return:
                Quantity += quantity;
                break;
            case MovementType.Exit:
            case MovementType.Sales:
                if (quantity > Quantity)
                    throw new BadRequestException("A quantidade em estoque nÃ£o pode ser negativa.");
                Quantity -= quantity;
                break;
            case MovementType.Adjustment:
                Quantity = quantity;
                break;
            default:
                throw new BadRequestException("Tipo de movimento invÃ¡lido.");
        }
    }

    public void RevertMovementStock(StockMovement movement)
    {
        var quantity = (decimal)movement.Quantity;

        switch (movement.MovementType)
        {
            case MovementType.Entry:
            case MovementType.Return:
                if (quantity > Quantity)
                    throw new BadRequestException("Ao reverter esta entrada, o estoque ficaria negativo.");
                Quantity -= quantity;
                break;
            case MovementType.Exit:
            case MovementType.Sales:
                Quantity += quantity;
                break;
            case MovementType.Adjustment:
                throw new BadRequestException("Não é possível reverter movimentações do tipo Ajuste, pois o saldo anterior é desconhecido.");
            default:
                throw new BadRequestException("Tipo de movimento inválido.");
        }
    }
}

