using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("orders")]
public class Order : BaseModel
{
    [ForeignKey("clients"), Required(ErrorMessage = "O Cliente é obrigatório.")]
    public Guid ClientId { get; private set; }
    public virtual Client? Client { get; private set; }

    [ForeignKey("users"), Required(ErrorMessage = "O Vendedor é obrigatório.")]
    public Guid UserId { get; private set; }
    public virtual User? User { get; private set; }

    [Required(ErrorMessage = "O status do pedido é obrigatório.")]
    public OrderStatus Status { get; private set; } = OrderStatus.Budget;

    [Required(ErrorMessage = "O tipo de pedido é obrigatório.")]
    public OrderType Type { get; private set; }

    public string? LossReason { get; private set; }

    public DateTime? UpdateAt { get; private set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O valor total não pode ser negativo."), Required(ErrorMessage = "O valor total é obrigatório.")]
    public decimal TotalAmount { get; private set; }

    [Range(0.0, double.MaxValue, ErrorMessage = "O valor do desconto não pode ser negativo.")]
    public decimal TotalDiscount { get; private set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = [];
    public virtual ICollection<Sale> Sales { get; set; } = [];

    public override string Preposition => "o";
    public override string Singular => "pedido";
    public override string Plural => "pedidos";

    private Order() : base() { }

    public Order(CreateOrderDto dto) : base()
    {
        ClientId = dto.ClientId;
        UserId = dto.UserId;
        Type = dto.Type;
        Status = dto.Type == OrderType.Sale ? OrderStatus.PendingPayment : OrderStatus.Budget;
        UpdateAt = DateTime.UtcNow;
    }

    public void UpdateStatus(OrderStatus newStatus, string? reason = null)
    {
        if (Status == newStatus)
            throw new BadRequestException($"O pedido já está com o status {newStatus}.");

        if (Status == OrderStatus.Canceled || Status == OrderStatus.Refunded)
            throw new BadRequestException("Não é possível atualizar o status de um pedido que já foi cancelado ou reembolsado.");

        if (newStatus == OrderStatus.Canceled)
        {
            if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Budget)
                throw new BadRequestException("Apenas pedidos pendentes ou orçamentos podem ser cancelados.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new BadRequestException("É obrigatório informar o motivo ao cancelar um pedido.");
        }
        else if (newStatus == OrderStatus.Refunded)
        {
            if (Status != OrderStatus.PaymentConfirmed)
                throw new BadRequestException("Apenas pedidos finalizados podem ser reembolsados.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new BadRequestException("É obrigatório informar o motivo ao reembolsar um pedido.");

            if (UpdateAt.HasValue)
            {
                var daysSincePayment = (DateTime.UtcNow - UpdateAt.Value).TotalDays;
                if (daysSincePayment > 7)
                    throw new BadRequestException("O pedido só pode ser reembolsado até 7 dias após o pagamento.");
            }
        }

        else if (newStatus == OrderStatus.PaymentConfirmed)
        {
            if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Budget)
                throw new BadRequestException("Apenas pedidos pendentes ou orçamentos podem ter o pagamento confirmado.");
        }

        Status = newStatus;
        LossReason = reason;
        UpdateAt = DateTime.UtcNow;
    }

    public void SetTotals(decimal totalAmount, decimal totalDiscount)
    {
        TotalAmount = totalAmount;
        TotalDiscount = totalDiscount;
        UpdateAt = DateTime.UtcNow;
    }
}
