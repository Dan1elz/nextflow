namespace Nextflow.Domain.Enums;

public enum OrderStatus : byte
{
    Budget = 1,           // Orçamento
    PendingPayment = 2,   // Aguardando pagamento
    PaymentConfirmed = 3, // Pagamento confirmado
    Canceled = 4,         // Pedido cancelado
    Refunded = 5          // Pedido reembolsado
}
