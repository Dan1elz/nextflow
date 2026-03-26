using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models.Base;

using Nextflow.Domain.Interfaces.Models;

namespace Nextflow.Domain.Models;

[Table("payments")]
public class Payment : BaseModel, IDeletable
{
    public bool IsActive { get; set; } = true;

    [ForeignKey("sales"), Required(ErrorMessage = "A Venda é obrigatória.")]
    public Guid SaleId { get; private set; }
    public virtual Sale? Sale { get; set; }

    [Required(ErrorMessage = "O valor do pagamento é obrigatório.")]
    public double Amount { get; private set; }

    [Column("Method")]
    [Required(ErrorMessage = "O método de pagamento é obrigatório.")]
    public PaymentMethod PaymentMethod { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "pagamento";
    public override string Plural => "pagamentos";

    public void Delete() => IsActive = false;
    public void Reactivate() => IsActive = true;

    private Payment() : base() { }

    public Payment(CreatePaymentDto dto) : base()
    {
        SaleId = dto.SaleId;
        Amount = (double)dto.Amount;
        PaymentMethod = dto.PaymentMethod;
    }
}
