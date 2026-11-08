namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Índice sazonal de um produto num mês do calendário (F3.1, RN-DF03).</summary>
public record ProductSeasonalityDto(Guid ProductId, short Month, decimal SeasonalIndex, short MonthsOfHistory);
