using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("purchase_orders")]
public class PurchaseOrder : BaseModel, IUpdatable<UpdatePurchaseOrderDto>, IDeletable
{
    [ForeignKey("suppliers"), Required(ErrorMessage = "O Fornecedor é obrigatório.")]
    public Guid SupplierId { get; private set; }
    public virtual Supplier? Supplier { get; private set; }

    [Required(ErrorMessage = "O status da compra é obrigatório.")]
    public PurchaseStatus PurchaseStatus { get; private set; } = PurchaseStatus.Budget;

    [Required(ErrorMessage = "O valor total é obrigatório.")]
    public decimal TotalAmount { get; private set; }

    public string? Note { get; private set; }

    public DateTime? UpdateAt { get; private set; }
    public bool Active { get; private set; } = true;

    [JsonIgnore]
    public virtual ICollection<PurchaseItem> PurchaseItems { get; set; } = [];

    public override string Preposition => "o";
    public override string Singular => "pedido de compra";
    public override string Plural => "pedidos de compra";

    private PurchaseOrder() : base() { }

    public PurchaseOrder(Guid supplierId, string? note = null) : base()
    {
        SupplierId = supplierId;
        Note = note;
    }

    public void Update(UpdatePurchaseOrderDto dto)
    {
        if (dto.Status.HasValue) PurchaseStatus = dto.Status.Value;
        if (dto.Note != null) Note = dto.Note;
        UpdateAt = DateTime.UtcNow;
    }

    public void SetTotalAmount(decimal totalAmount)
    {
        TotalAmount = totalAmount;
        UpdateAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        Active = false;
        PurchaseStatus = PurchaseStatus.Canceled;
        UpdateAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Active = true;
        UpdateAt = DateTime.UtcNow;
    }

    // Required by IDeletable but usually IDeletable uses IsActive. 
    // The user asked for 'Active'. I'll mapping it.
    [NotMapped]
    public bool IsActive { get => Active; set => Active = value; }
}
