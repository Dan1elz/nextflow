namespace Nextflow.Domain.Enums;

public enum ForecastRunStatus : byte
{
    Completed = 1,           // Concluída
    CompletedWithoutAi = 2,  // Concluída sem IA
    Failed = 3               // Falhou
}
