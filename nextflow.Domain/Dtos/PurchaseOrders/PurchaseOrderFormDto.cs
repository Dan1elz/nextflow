using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.PurchaseOrders;

/// <summary>Formulário de edição (T-CP2). <see cref="Options"/> traz os locais ativos de lojas ativas para o combo de entrega.</summary>
public class PurchaseOrderFormDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public uint Version { get; set; }
    public PurchaseStatusEnum Status { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid ReceiveLocationId { get; set; }
    public string? Note { get; set; }
    public int LeadTimeDays { get; set; }
    public DateOnly? ExpectedOn { get; set; }
    public decimal Total { get; set; }
    public List<PurchaseOrderFormItemDto> Items { get; set; } = [];
    public PurchaseOrderFormOptionsDto Options { get; set; } = new();

    public PurchaseOrderFormDto() { }

    /// <param name="entity">Com fornecedor carregado.</param>
    public PurchaseOrderFormDto(PurchaseOrder entity, List<PurchaseOrderFormItemDto> items, List<LocationOptionDto> locations)
    {
        Id = entity.Id;
        Number = entity.Number;
        Version = entity.Version;
        Status = entity.Status;
        SupplierId = entity.SupplierId;
        SupplierName = entity.Supplier != null ? entity.Supplier.Name : string.Empty;
        ReceiveLocationId = entity.ReceiveLocationId;
        Note = entity.Note;
        LeadTimeDays = entity.LeadTimeDays;
        ExpectedOn = entity.ExpectedOn;
        Total = entity.Total;
        Items = items;
        Options = new PurchaseOrderFormOptionsDto { Locations = locations };
    }
}
