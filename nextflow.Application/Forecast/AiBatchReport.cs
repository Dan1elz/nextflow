using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>Andamento de um lote da IA (RN-DF20), reportado ao serviço para gravar em <c>forecast_ai_batches</c>.</summary>
public sealed record AiBatchReport(int Number, IReadOnlyList<Guid> ProductIds, AiBatchStatusEnum Status, string? Error, int DurationMs);
