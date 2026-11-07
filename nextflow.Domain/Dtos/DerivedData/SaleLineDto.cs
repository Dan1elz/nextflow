namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Item de pedido Pago lido para o recálculo (F3.1). O instante é UTC; o mês de negócio sai do <c>IBusinessClock</c>.</summary>
public record SaleLineDto(Guid ProductId, DateTime SoldAtUtc, decimal Quantity, bool OnPromotion, decimal LineTotal);
