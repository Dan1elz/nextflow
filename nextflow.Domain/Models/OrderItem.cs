using Nextflow.Domain.Interfaces.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("order_items")]
public class OrderItem : BaseModel, IDeletable
{
    [ForeignKey("orders"), Required(ErrorMessage = "A Ordem de Venda Ã© obrigatÃ³ria.")]
    public Guid OrderId { get; private set; }
    public virtual Order? Order { get; set; }

    [ForeignKey("products"), Required(ErrorMessage = "O Produto Ã© obrigatÃ³rio.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero."), Required(ErrorMessage = "A quantidade Ã© obrigatÃ³ria.")]
    public decimal Quantity { get; private set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O preÃ§o unitÃ¡rio nÃ£o pode ser negativo."), Required(ErrorMessage = "O preÃ§o unitÃ¡rio Ã© obrigatÃ³rio.")]
    public decimal UnitPrice { get; private set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O desconto nÃ£o pode ser negativo."), Required(ErrorMessage = "O desconto Ã© obrigatÃ³rio.")]
    public decimal Discount { get; private set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O valor total nÃ£o pode ser negativo."), Required(ErrorMessage = "O valor total Ã© obrigatÃ³rio.")]
    public decimal TotalPrice { get; private set; }

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
    public override string Singular => "item de pedido";
    public override string Plural => "itens de pedido";

    private OrderItem() : base() { }

    public OrderItem(CreateOrderItemDto dto) : base()
    {
        OrderId = dto.OrderId;
        ProductId = dto.ProductId;
        Quantity = dto.Quantity;
        Discount = dto.Discount;
    }
    public void Update(UpdateOrderItemDto dto)
    {
        Quantity = dto.Quantity;
        Discount = dto.Discount;
        Update();
    }

    public void SetPricing(decimal unitPrice, decimal totalPrice)
    {
        UnitPrice = unitPrice;
        TotalPrice = totalPrice;
    }
}
