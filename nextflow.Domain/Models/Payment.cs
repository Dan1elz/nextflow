using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("payments")]
public class Payment : BaseModel
{
    [ForeignKey("sales"), Required(ErrorMessage = "A Venda é obrigatória.")]
    public Guid SaleId { get; private set; }
    public virtual Sale? Sale { get; set; }

    [Required(ErrorMessage = "O valor do pagamento é obrigatório.")]
    public double Amount { get; private set; }

    [Required(ErrorMessage = "O método de pagamento é obrigatório.")]
    public PaymentMethod PaymentMethod { get; private set; }

    public override string Preposition => "o";
    public override string Singular => "pagamento";
    public override string Plural => "pagamentos";

    private Payment() : base() { }

    public Payment(CreatePaymentDto dto) : base()
    {
        SaleId = dto.SaleId;
        Amount = (double)dto.Amount;
        PaymentMethod = dto.PaymentMethod;
    }
}
