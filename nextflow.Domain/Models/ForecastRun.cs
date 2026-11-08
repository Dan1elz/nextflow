using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Models;

/// <summary>
/// Uma análise de previsão (RN-DF18, RN-DF23). Nasce <c>Running</c>; o motor (F3.2) define o horizonte automático (RN-DF17),
/// acompanha os lotes da IA e fecha como <c>Completed</c>, <c>CompletedWithoutAi</c> ou <c>Failed</c>. As datas vêm do
/// <c>IBusinessClock</c>: <see cref="ReferenceDate"/> é o dia de negócio da análise.
/// </summary>
public class ForecastRun : BaseModel
{
    public ForecastTriggerEnum Trigger { get; private set; }
    public Guid? RequestedById { get; private set; }
    /// <summary>Zero enquanto o motor ainda não leu os prazos de entrega; depois, de 1 a 6 (RN-DF17).</summary>
    public short HorizonMonths { get; private set; }
    public ForecastRunStatusEnum Status { get; private set; } = ForecastRunStatusEnum.Running;
    public DateOnly ReferenceDate { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public int? DurationMs { get; private set; }
    public string? AiProvider { get; private set; }
    public string? AiModel { get; private set; }
    public string? Summary { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int ProductsAnalyzed { get; private set; }
    public int ProductsSentToAi { get; private set; }
    public int BatchesTotal { get; private set; }
    public int BatchesCompleted { get; private set; }
    public int BatchesFailed { get; private set; }

    public virtual User? RequestedBy { get; private set; }
    public virtual ICollection<ForecastAiBatch> Batches { get; private set; } = [];
    public virtual ICollection<ForecastRecommendation> Recommendations { get; private set; } = [];
    public virtual ICollection<ForecastProjection> Projections { get; private set; } = [];

    public override string Preposition => "a";
    public override string Singular => "análise de previsão";
    public override string Plural => "análises de previsão";

    private ForecastRun() { }

    public ForecastRun(StartForecastRunDto dto, DateOnly referenceDate, DateTime startedAtUtc)
    {
        Trigger = dto.Trigger;
        RequestedById = dto.RequestedById;
        ReferenceDate = referenceDate;
        StartedAt = startedAtUtc;
    }

    public bool IsRunning => Status == ForecastRunStatusEnum.Running;

    /// <summary>Resultado do cálculo antes da IA: horizonte, produtos cobertos e quantos lotes serão enviados.</summary>
    public void BeginAnalysis(short horizonMonths, int productsAnalyzed, int productsSentToAi, int batchesTotal)
    {
        HorizonMonths = horizonMonths;
        ProductsAnalyzed = productsAnalyzed;
        ProductsSentToAi = productsSentToAi;
        BatchesTotal = batchesTotal;
    }

    /// <summary>Progresso dos lotes ("Lote 3 de 8", T-DF2).</summary>
    public void RecordBatch(bool completed)
    {
        if (completed)
            BatchesCompleted++;
        else
            BatchesFailed++;
    }

    public void Complete(ForecastRunStatusEnum status, string summary, string? errorMessage, string? aiProvider, string? aiModel, DateTime finishedAtUtc)
    {
        Status = status;
        Summary = summary;
        ErrorMessage = errorMessage;
        AiProvider = aiProvider;
        AiModel = aiModel;
        Finish(finishedAtUtc);
    }

    public void Fail(string errorMessage, DateTime finishedAtUtc)
    {
        Status = ForecastRunStatusEnum.Failed;
        ErrorMessage = errorMessage;
        Finish(finishedAtUtc);
    }

    private void Finish(DateTime finishedAtUtc)
    {
        FinishedAt = finishedAtUtc;
        DurationMs = (int)Math.Max(0, (finishedAtUtc - StartedAt).TotalMilliseconds);
    }
}
