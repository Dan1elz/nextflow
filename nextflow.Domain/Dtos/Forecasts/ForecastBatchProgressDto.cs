namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Progresso dos lotes da IA ("Lote 3 de 8", RN-DF20).</summary>
public class ForecastBatchProgressDto
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }

    public ForecastBatchProgressDto() { }

    public ForecastBatchProgressDto(int total, int completed, int failed)
    {
        Total = total;
        Completed = completed;
        Failed = failed;
    }
}
