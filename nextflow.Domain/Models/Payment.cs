using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>Pagamento à vista do pedido (RN-VD18). Nunca é editado nem apagado; o valor é o líquido, sem troco.</summary>
public class Payment : BaseModel
{
    public Guid OrderId { get; private set; }
    public PaymentMethodEnum Method { get; private set; }
    public decimal Amount { get; private set; }

    public virtual Order Order { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "pagamento";
    public override string Plural => "pagamentos";

    private Payment() { }

    public Payment(QuickSalePaymentDto dto)
    {
        Method = dto.Method;
        Amount = dto.Amount;
    }
}
