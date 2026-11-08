namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Prazo real de um pedido recebido (recebimento menos envio, RN-CP08): amostra do prazo de entrega do fornecedor.</summary>
public record ReceivedLeadTimeDto(Guid SupplierId, int Days, DateTime ReceivedAt);
