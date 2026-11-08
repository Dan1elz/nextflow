namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Saldo de lote vencendo num local (consulta do painel e do resumo diário).</summary>
public record ExpiringLotRowDto(Guid ProductId, Guid VariantId, string ProductName, Guid LotId, string LotCode, Guid LocationId,
    string LocationName, DateOnly ExpiresOn, decimal Quantity, decimal? UnitCost,
    Guid StoreId, string StoreName);
