using Nextflow.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Models;

[Table("sales")]
public class Sale : BaseModel
{
    [ForeignKey("user"), Required(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid UserId { get; private set; }
    public virtual User? User { get; set; }

    [ForeignKey("orders"), Required(ErrorMessage = "A Ordem de Venda é obrigatória.")]
    public Guid OrderId { get; private set; }
    public virtual Order? Order { get; set; }

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
