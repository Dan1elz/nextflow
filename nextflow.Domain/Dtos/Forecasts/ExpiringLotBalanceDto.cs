namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Saldo positivo de uma variação num lote, somando os locais: base do vencimento próximo da previsão (RN-DF07). Só leitura.</summary>
public record ExpiringLotBalanceDto(Guid VariantId, DateOnly ExpiresOn, decimal Quantity);
