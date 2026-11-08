namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Variação × local abaixo do mínimo (RN-CE33), linha crua do repositório (o rótulo da variação vem depois).</summary>
public record LowStockRowDto(Guid ProductId, string ProductName, Guid VariantId, string LocationName, decimal Available, decimal MinQuantity);
