using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>
/// Detalhe do pedido (T-VD5). Na F2.4 sai da venda rápida; <see cref="AllowedActions"/> fica vazio até o reembolso (F2.6).
/// Totais, troco e status vêm prontos: o front não recalcula.
/// </summary>
public class OrderDetailDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public OrderRefDto? Store { get; set; }
    public OrderRefDto? SaleLocation { get; set; }
    public OrderClientDto? Client { get; set; }
    public OrderRefDto? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<OrderItemDetailDto> Items { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal Total { get; set; }
    public decimal? CashReceived { get; set; }
    public decimal? Change { get; set; }
    public List<OrderPaymentDto> Payments { get; set; } = [];
    public List<string> AllowedActions { get; set; } = [];

    public OrderDetailDto() { }

    public OrderDetailDto(Order entity)
    {
        Id = entity.Id;
        Number = entity.Number;
        Status = entity.Status.GetApiName();
        Channel = entity.Channel.GetApiName();
        if (entity.Store != null)
            Store = new OrderRefDto(entity.Store.Id, entity.Store.Name);
        if (entity.SaleLocation != null)
            SaleLocation = new OrderRefDto(entity.SaleLocation.Id, entity.SaleLocation.Name);
        if (entity.Client != null)
            Client = new OrderClientDto(entity.Client);
        if (entity.User != null)
            User = new OrderRefDto(entity.User.Id, entity.User.FullName);
        CreatedAt = entity.CreatedAt;
        PaidAt = entity.PaidAt;
        Items = [.. entity.Items.OrderBy(i => i.Position).Select(i => new OrderItemDetailDto(i))];
        Subtotal = entity.Subtotal;
        DiscountTotal = entity.DiscountTotal;
        Total = entity.Total;
        CashReceived = entity.CashReceived;
        Change = entity.Change;
        Payments = [.. entity.Payments.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => new OrderPaymentDto(p))];
    }
}
