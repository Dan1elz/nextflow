namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Saldo positivo de um lote num local, com validade na janela (L-DF4), linha crua do repositório.</summary>
public record ExpiringLotRowDto(Guid VariantId, string ProductName, string LotCode, string LocationName, DateOnly ExpiresOn, decimal Quantity, decimal? UnitCost);
