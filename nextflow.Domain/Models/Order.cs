using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;


[Table("orders")]
public class Order : BaseModel, IUpdatable<UpdateOrderDto>, IDeletable
{
    [ForeignKey("clients"), Required(ErrorMessage = "O Cliente Ã© obrigatÃ³rio.")]
    public Guid ClientId { get; private set; }
    public virtual Client? Client { get; private set; }

    [Required(ErrorMessage = "O status do pedido Ã© obrigatÃ³rio.")]
    public OrderStatus Status { get; private set; } = OrderStatus.PendingPayment;

    [Range(0.0, double.MaxValue, ErrorMessage = "O valor total nÃ£o pode ser negativo."), Required(ErrorMessage = "O valor total Ã© obrigatÃ³rio.")]
    public decimal TotalAmount { get; private set; }
    [Range(0.0, double.MaxValue, ErrorMessage = "O valor do desconto nÃ£o pode ser negativo.")]
    public decimal DiscountAmount { get; private set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];
    public virtual ICollection<Sale> Sales { get; set; } = [];

    public DateTime? UpdateAt { get; private set; }
    public bool IsActive { get; set; } = true;

    public void Update()
    {
        UpdateAt = DateTime.UtcNow;
    }
    public void Delete()
    {
        if (Status != OrderStatus.PendingPayment)
            throw new BadRequestException("Apenas pedidos com status 'Aguardando Pagamento' podem ser cancelados.");

        IsActive = false;
        UpdateAt = DateTime.UtcNow;
        Status = OrderStatus.Canceled;
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
    public override string Singular => "pedido";
    public override string Plural => "pedidos";

    private Order() : base() { }
    public Order(CreateOrderDto dto) : base()
    {
        ClientId = dto.ClientId;

    }
    public void Update(UpdateOrderDto dto)
    {
        // A lÃ³gica de atualizaÃ§Ã£o estÃ¡ nos hooks do UpdateOrderUseCase
        // Este mÃ©todo apenas marca a entidade como atualizada
        Update();
    }

    public void UpdateStatus(OrderStatus status)
    {
        Status = status;
        Update();
    }

    public void SetTotals(decimal totalAmount, decimal discountAmount)
    {
        TotalAmount = totalAmount;
        DiscountAmount = discountAmount;
    }
}

