namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Lote de produtos que será enviado à IA, em ordem sequencial (RN-DF20).</summary>
public record NewForecastAiBatchDto(Guid ForecastRunId, short BatchNumber, IReadOnlyList<Guid> ProductIds);
