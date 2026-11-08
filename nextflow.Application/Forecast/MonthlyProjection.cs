namespace Nextflow.Application.Forecast;

/// <summary>Demanda projetada de um mês do horizonte e o índice sazonal usado.</summary>
public sealed record MonthlyProjection(YearMonth Month, decimal Quantity, decimal SeasonalIndex);
