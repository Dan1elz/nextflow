namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Resumo do recálculo: quantas linhas foram gravadas em cada tabela derivada.</summary>
public record DerivedDataRebuildDto(int SalesMonthlyRows, int SnapshotRows, int SeasonalityRows);
