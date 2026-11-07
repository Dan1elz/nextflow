using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Linha da listagem (T-CP1). Recebimento e lead time real chegam na F2.8; origem da IA na F3.4.</summary>
public class PurchaseOrderListItemDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ReceiveLocationId { get; set; }
    public string ReceiveLocationName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public PurchaseStatusEnum Status { get; set; }
    public int ItemCount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateOnly? ExpectedOn { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public int? RealLeadTimeDays { get; set; }
    public bool IsLate { get; set; }
    public bool FromForecast { get; set; }

    public PurchaseOrderListItemDto() { }

    /// <param name="entity">Com fornecedor, local, loja e itens carregados.</param>
    public PurchaseOrderListItemDto(PurchaseOrder entity, DateOnly today)
    {
        Id = entity.Id;
        Number = entity.Number;
        SupplierId = entity.SupplierId;
        SupplierName = entity.Supplier != null ? entity.Supplier.Name : string.Empty;
        ReceiveLocationId = entity.ReceiveLocationId;
        ReceiveLocationName = entity.ReceiveLocation != null ? entity.ReceiveLocation.Name : string.Empty;
        StoreName = entity.Store != null ? entity.Store.Name : string.Empty;
        Status = entity.Status;
        ItemCount = entity.Items.Count;
        Total = entity.Total;
        CreatedAt = entity.CreatedAt;
        SentAt = entity.SentAt;
        ExpectedOn = entity.ExpectedOn;
        ReceivedAt = entity.ReceivedAt;
        RealLeadTimeDays = entity.RealLeadTimeDays;
        IsLate = entity.IsLate(today);
    }
}
