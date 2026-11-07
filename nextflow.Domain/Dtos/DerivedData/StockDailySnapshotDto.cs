namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Saldo de fechamento de um dia por variação × local (F3.1). Zero (ou menos) = dia sem estoque.</summary>
public record StockDailySnapshotDto(Guid VariantId, Guid LocationId, DateOnly Day, decimal ClosingQuantity);
