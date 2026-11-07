using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>
/// Pedido de compra (RN-CP07): Orçamento → Pendente (enviado, RN-CP08) → Recebido (F2.8), ou Cancelado. Agregado dono dos
/// itens, sincronizados no mesmo commit. O prazo é o maior prazo efetivo entre os itens (RN-CE60) e a previsão conta do envio,
/// ou da criação enquanto é orçamento. <see cref="Version"/> é o <c>xmin</c> do Postgres.
/// </summary>
public class PurchaseOrder : BaseModel, IVersioned
{
    public int Number { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid ReceiveLocationId { get; private set; }
    public PurchaseStatusEnum Status { get; private set; } = PurchaseStatusEnum.Budget;
    public string? Note { get; private set; }
    public decimal Total { get; private set; }
    public int LeadTimeDays { get; private set; }
    public DateOnly ExpectedOn { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }
    public DateTime? CanceledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public uint Version { get; private set; }

    public virtual Supplier Supplier { get; private set; } = null!;
    public virtual Store Store { get; private set; } = null!;
    public virtual StockLocation ReceiveLocation { get; private set; } = null!;
    public virtual User CreatedBy { get; private set; } = null!;
    public virtual ICollection<PurchaseItem> Items { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "pedido de compra";
    public override string Plural => "pedidos de compra";

    private PurchaseOrder() { }

    /// <param name="createdOn">Dia de negócio da criação: base da previsão enquanto não é enviado.</param>
    public PurchaseOrder(SavePurchaseOrderDto dto, Guid storeId, Guid createdById, int leadTimeDays, DateOnly createdOn)
    {
        CreatedById = createdById;
        Apply(dto, storeId, leadTimeDays, createdOn);
    }

    /// <summary>Só Orçamento e Pendente mudam (RN-CP03).</summary>
    public bool IsOpen => Status is PurchaseStatusEnum.Budget or PurchaseStatusEnum.Pending;

    /// <summary>Pendente com a previsão vencida (T-CP1). Orçamento ainda não foi pedido, então não atrasa.</summary>
    public bool IsLate(DateOnly today) => Status == PurchaseStatusEnum.Pending && ExpectedOn < today;

    /// <summary>Prazo do pedido: o maior prazo efetivo dos itens (o do vínculo, senão o do fornecedor, RN-CE60).</summary>
    public static int EffectiveLeadTime(int supplierLeadTimeDays, IEnumerable<int?> itemLeadTimeDays)
    {
        var effective = itemLeadTimeDays.Select(days => days ?? supplierLeadTimeDays).ToList();
        return effective.Count == 0 ? supplierLeadTimeDays : effective.Max();
    }

    /// <param name="baseDate">Dia de negócio do envio, ou da criação enquanto é orçamento.</param>
    public void Update(SavePurchaseOrderDto dto, Guid storeId, int leadTimeDays, DateOnly baseDate)
    {
        Apply(dto, storeId, leadTimeDays, baseDate);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Enviar ao fornecedor (RN-CP08): a previsão passa a contar do dia do envio.</summary>
    public void Send(DateTime utcNow, DateOnly today)
    {
        Status = PurchaseStatusEnum.Pending;
        SentAt = utcNow;
        ExpectedOn = today.AddDays(LeadTimeDays);
        UpdatedAt = utcNow;
    }

    /// <summary>Cancelar (T-CP5): nada entrou no estoque, então não há movimentação.</summary>
    public void Cancel(string reason, DateTime utcNow)
    {
        Status = PurchaseStatusEnum.Canceled;
        CancelReason = reason.Trim();
        CanceledAt = utcNow;
        UpdatedAt = utcNow;
    }

    private void Apply(SavePurchaseOrderDto dto, Guid storeId, int leadTimeDays, DateOnly baseDate)
    {
        SupplierId = dto.SupplierId;
        StoreId = storeId;
        ReceiveLocationId = dto.ReceiveLocationId;
        Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        LeadTimeDays = leadTimeDays;
        ExpectedOn = baseDate.AddDays(leadTimeDays);
        SyncItems(dto.Items);
        Total = Items.Sum(i => i.Subtotal);
    }

    /// <summary>A linha com id preserva o id; a que saiu do formulário é apagada; a nova entra (T-CP2).</summary>
    private void SyncItems(List<SavePurchaseItemDto> items)
    {
        var keptIds = items.Where(i => i.Id is not null).Select(i => i.Id!.Value).ToHashSet();
        foreach (var removed in Items.Where(i => !keptIds.Contains(i.Id)).ToList())
            Items.Remove(removed);

        for (var index = 0; index < items.Count; index++)
        {
            var dto = items[index];
            var existing = dto.Id is { } id ? Items.FirstOrDefault(i => i.Id == id) : null;
            if (existing is null)
                Items.Add(new PurchaseItem(dto, Id, (short)index));
            else
                existing.Update(dto, (short)index);
        }
    }
}
