using Nextflow.Domain.Interfaces.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("sales")]
public class Sale : BaseModel, IDeletable
{
    [ForeignKey("user"), Required(ErrorMessage = "O UsuÃ¡rio Ã© obrigatÃ³rio.")]
    public Guid UserId { get; private set; }
    public virtual User? User { get; set; }

    [ForeignKey("orders"), Required(ErrorMessage = "A Ordem de Venda Ã© obrigatÃ³ria.")]
    public Guid OrderId { get; private set; }
    public virtual Order? Order { get; set; }
    public virtual ICollection<Payment> Payments { get; set; } = [];

    public DateTime? UpdateAt { get; private set; }
    public bool IsActive { get; set; } = true;

    public void Update()
    {
        UpdateAt = DateTime.UtcNow;
    }
    public void Delete()
    {
        IsActive = false;
        UpdateAt = DateTime.UtcNow;
    }
    public void Reactivate()
    {
        if (!IsActive)
        {
            IsActive = true;
            UpdateAt = DateTime.UtcNow;
        }
    }

    public override string Preposition => "a";
    public override string Singular => "venda";
    public override string Plural => "vendas";

    private Sale() : base() { }

    public Sale(CreateSaleDto dto) : base()
    {
        UserId = dto.UserId;
        OrderId = dto.OrderId;
    }
}

