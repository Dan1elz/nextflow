using Nextflow.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;

using Nextflow.Domain.Interfaces.Models;

namespace Nextflow.Domain.Models;

[Table("sales")]
public class Sale : BaseModel, IDeletable
{
    public bool IsActive { get; set; } = true;

    [ForeignKey("user"), Required(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid UserId { get; private set; }
    public virtual User? User { get; set; }

    [ForeignKey("orders"), Required(ErrorMessage = "A Ordem de Venda é obrigatória.")]
    public Guid OrderId { get; private set; }
    public virtual Order? Order { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = [];

    public override string Preposition => "a";
    public override string Singular => "venda";
    public override string Plural => "vendas";

    public void Delete() => IsActive = false;
    public void Reactivate() => IsActive = true;

    private Sale() : base() { }

    public Sale(CreateSaleDto dto) : base()
    {
        UserId = dto.UserId;
        OrderId = dto.OrderId;
    }
}
