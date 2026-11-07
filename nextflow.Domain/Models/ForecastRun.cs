using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

/// <summary>Execução (análise) de previsão de demanda.</summary>
[Table("forecast_runs")]
public class ForecastRun : BaseModel
{
    [ForeignKey("users"), Required(ErrorMessage = "O Usuário é obrigatório.")]
    public Guid RequestedByUserId { get; private set; }
    public virtual User? RequestedByUser { get; private set; }

    [Range(1, 3, ErrorMessage = "O horizonte deve estar entre 1 e 3 meses.")]
    public int HorizonMonths { get; private set; }

    public ForecastRunStatus Status { get; private set; } = ForecastRunStatus.Completed;

    public string? AiProvider { get; private set; }
    public string? AiModel { get; private set; }

    /// <summary>Resumo geral em pt-BR (da IA ou de template).</summary>
    public string? Summary { get; private set; }

    /// <summary>Motivo do fallback/falha.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Payload enviado à IA (auditoria).</summary>
    public string? InputSnapshotJson { get; private set; }

    public int DurationMs { get; private set; }
    public int ProductsAnalyzed { get; private set; }

    public virtual ICollection<ForecastRecommendation> Recommendations { get; set; } = [];
    public virtual ICollection<ForecastProjection> Projections { get; set; } = [];

    public override string Preposition => "a";
    public override string Singular => "análise de previsão";
    public override string Plural => "análises de previsão";

    private ForecastRun() : base() { }

    public ForecastRun(Guid requestedByUserId, int horizonMonths) : base()
    {
        RequestedByUserId = requestedByUserId;
        HorizonMonths = horizonMonths;
    }

    public void SetAiInfo(string? aiProvider, string? aiModel)
    {
        AiProvider = aiProvider;
        AiModel = aiModel;
    }

    public void SetInputSnapshot(string? inputSnapshotJson)
    {
        InputSnapshotJson = inputSnapshotJson;
    }

    public void Complete(ForecastRunStatus status, string? summary, string? errorMessage, int durationMs, int productsAnalyzed)
    {
        Status = status;
        Summary = summary;
        ErrorMessage = errorMessage;
        DurationMs = durationMs;
        ProductsAnalyzed = productsAnalyzed;
    }

    public void Fail(string errorMessage, int durationMs)
    {
        Status = ForecastRunStatus.Failed;
        ErrorMessage = errorMessage;
        DurationMs = durationMs;
    }
}
