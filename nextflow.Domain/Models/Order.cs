using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Pedido: orçamento ou venda paga (RN-VD16). A venda rápida de balcão (T-VD2) nasce <see cref="OrderStatusEnum.Paid"/>
/// no canal <see cref="OrderChannelEnum.Counter"/>. Totais saem dos itens: subtotal bruto, descontos em R$ por item
/// (RN-VD03) e total líquido. O número vem de uma sequência do banco.
/// </summary>
public class Order : BaseModel
{
    public int Number { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid SaleLocationId { get; private set; }
    public Guid? ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public OrderStatusEnum Status { get; private set; }
    public OrderChannelEnum Channel { get; private set; }
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal Total { get; private set; }
    /// <summary>Valor entregue em dinheiro, só para o troco.</summary>
    public decimal? CashReceived { get; private set; }
    public decimal? Change { get; private set; }
    public DateTime? PaidAt { get; private set; }

    public virtual Store Store { get; private set; } = null!;
    public virtual StockLocation SaleLocation { get; private set; } = null!;
    public virtual Client? Client { get; private set; }
    public virtual User User { get; private set; } = null!;
    public virtual ICollection<OrderItem> Items { get; private set; } = [];
    public virtual ICollection<Payment> Payments { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "pedido";
    public override string Plural => "pedidos";

    private Order() { }

    /// <summary>Venda rápida já paga; itens e pagamentos entram por <see cref="AddItem"/> e <see cref="AddPayment"/>.</summary>
    public Order(QuickSaleDto dto, Guid saleLocationId, Guid userId, DateTime paidAt)
    {
        StoreId = dto.StoreId;
        SaleLocationId = saleLocationId;
        ClientId = dto.ClientId;
        UserId = userId;
        Status = OrderStatusEnum.Paid;
        Channel = OrderChannelEnum.Counter;
        PaidAt = paidAt;
    }

    public void AddItem(OrderItem item)
    {
        item.PlaceAt(Items.Count);
        Items.Add(item);
        Subtotal += item.GrossTotal;
        DiscountTotal += item.Discount;
        Total += item.LineTotal;
    }

    public void AddPayment(Payment payment) => Payments.Add(payment);

    /// <summary>Troco = recebido − pago em dinheiro (o pagamento em dinheiro grava o líquido).</summary>
    public void RecordCash(decimal cashReceived, decimal cashPaid)
    {
        CashReceived = cashReceived;
        Change = cashReceived - cashPaid;
    }
}
