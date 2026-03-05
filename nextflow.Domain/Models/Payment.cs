using Nextflow.Domain.Interfaces.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

[Table("payments")]
public class Payment : BaseModel, IDeletable
{
    [ForeignKey("sales"), Required(ErrorMessage = "A Venda Ã© obrigatÃ³ria.")]
    public Guid SaleId { get; private set; }
    public virtual Sale? Sale { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "O valor do pagamento deve ser maior que zero."), Required(ErrorMessage = "O valor do pagamento Ã© obrigatÃ³rio.")]
    public decimal Amount { get; private set; }

    [Required(ErrorMessage = "O mÃ©todo de pagamento Ã© obrigatÃ³rio.")]
    public PaymentMethod Method { get; private set; }

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

    public override string Preposition => "o";
    public override string Singular => "pagamento";
    public override string Plural => "pagamentos";

    private Payment() : base() { }

    public Payment(CreatePaymentDto dto) : base()
    {
        SaleId = dto.SaleId;
        Amount = dto.Amount;
        Method = dto.Method;
    }
}

