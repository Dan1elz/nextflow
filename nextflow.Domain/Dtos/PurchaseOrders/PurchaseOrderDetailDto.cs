using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Visualização do pedido (T-CP3) e resposta de criar, editar, enviar, cancelar e receber.</summary>
public class PurchaseOrderDetailDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public uint Version { get; set; }
    public PurchaseStatusEnum Status { get; set; }
    public PurchaseOrderSupplierDto Supplier { get; set; } = new();
    public LocationOptionDto ReceiveLocation { get; set; } = new();
    public string? Note { get; set; }
    public decimal Total { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateOnly? ExpectedOn { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedByName { get; set; }
    public int? RealLeadTimeDays { get; set; }
    public DateTime? CanceledAt { get; set; }
    public string? CancelReason { get; set; }
    public PurchaseOrderForecastDto? Forecast { get; set; }
    public List<PurchaseOrderDetailItemDto> Items { get; set; } = [];

    public PurchaseOrderDetailDto() { }

    /// <param name="entity">Com fornecedor, local e loja, autor e itens (variação, produto, vínculos, grade) carregados.</param>
    public PurchaseOrderDetailDto(PurchaseOrder entity)
    {
        Id = entity.Id;
        Number = entity.Number;
        Version = entity.Version;
        Status = entity.Status;
        if (entity.Supplier != null)
            Supplier = new PurchaseOrderSupplierDto(entity.Supplier);
        if (entity.ReceiveLocation != null)
            ReceiveLocation = new LocationOptionDto(entity.ReceiveLocation);
        Note = entity.Note;
        Total = entity.Total;
        LeadTimeDays = entity.LeadTimeDays;
        CreatedAt = entity.CreatedAt;
        CreatedByName = entity.CreatedBy != null ? $"{entity.CreatedBy.Name} {entity.CreatedBy.LastName}" : string.Empty;
        SentAt = entity.SentAt;
        ExpectedOn = entity.ExpectedOn;
        ReceivedAt = entity.ReceivedAt;
        ReceivedByName = entity.ReceivedBy != null ? $"{entity.ReceivedBy.Name} {entity.ReceivedBy.LastName}" : null;
        RealLeadTimeDays = entity.RealLeadTimeDays;
        CanceledAt = entity.CanceledAt;
        CancelReason = entity.CancelReason;
        Items = [.. entity.Items.OrderBy(i => i.SortOrder).Select(i => new PurchaseOrderDetailItemDto(i, entity.SupplierId))];
    }
}
