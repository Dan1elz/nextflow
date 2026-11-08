namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Variação × local abaixo do mínimo (consulta do painel e do resumo diário).</summary>
public record LowStockRowDto(Guid ProductId, string ProductName, Guid VariantId, Guid LocationId, string LocationName, decimal Available,
    decimal MinQuantity, Guid StoreId, string StoreName);
