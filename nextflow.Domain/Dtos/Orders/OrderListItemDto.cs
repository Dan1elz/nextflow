using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Linha da listagem de pedidos (T-VD3): quando, quem comprou, quanto e como pagou. Validade entra com o orçamento (F2.5).</summary>
public class OrderListItemDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Nulo = venda sem cliente identificado.</summary>
    public string? ClientName { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string SaleLocationName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    /// <summary>Formas de pagamento usadas, sem repetir, na ordem em que entraram.</summary>
    public List<string> PaymentMethods { get; set; } = [];

    public OrderListItemDto() { }

    /// <param name="entity">Com cliente, loja, local, vendedor, itens e pagamentos carregados.</param>
    public OrderListItemDto(Order entity)
    {
        Id = entity.Id;
        Number = entity.Number;
        Status = entity.Status.GetApiName();
        Channel = entity.Channel.GetApiName();
        CreatedAt = entity.CreatedAt;
        PaidAt = entity.PaidAt;
        ClientName = entity.Client?.FullName;
        StoreName = entity.Store != null ? entity.Store.Name : string.Empty;
        SaleLocationName = entity.SaleLocation != null ? entity.SaleLocation.Name : string.Empty;
        UserName = entity.User != null ? entity.User.FullName : string.Empty;
        ItemCount = entity.Items.Count;
        Total = entity.Total;
        PaymentMethods = [.. entity.Payments.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id)
            .Select(p => p.Method.GetApiName()).Distinct()];
    }
}
