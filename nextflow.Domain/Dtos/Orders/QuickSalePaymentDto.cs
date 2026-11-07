using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Orders;

/// <summary>Pagamento à vista da venda (RN-VD18, RN-VD20). O valor é o líquido, sem troco.</summary>
public class QuickSalePaymentDto
{
    public PaymentMethodEnum Method { get; set; }
    public decimal Amount { get; set; }
}
