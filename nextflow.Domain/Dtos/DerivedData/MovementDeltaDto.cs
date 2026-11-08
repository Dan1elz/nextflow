namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Movimentação do livro-razão reduzida ao que o saldo diário precisa (F3.1). <c>Quantity</c> já vem com sinal.</summary>
public record MovementDeltaDto(Guid VariantId, Guid LocationId, DateTime AtUtc, decimal Quantity);
