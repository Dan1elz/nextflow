namespace Nextflow.Application.Forecast;

/// <summary>Parte de uma quantidade que cabe a uma variação (rateio da grade, RN-DF21).</summary>
public sealed record VariantShare(Guid VariantId, decimal Quantity, decimal SharePercent);
