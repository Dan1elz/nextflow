using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Orders;

public class OrderPaymentDto
{
    public Guid Id { get; set; }
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }

    public OrderPaymentDto() { }

    public OrderPaymentDto(Payment entity)
    {
        Id = entity.Id;
        Method = entity.Method.GetApiName();
        Amount = entity.Amount;
        CreatedAt = entity.CreatedAt;
    }
}
