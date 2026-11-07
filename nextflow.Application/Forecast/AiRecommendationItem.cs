namespace Nextflow.Application.Forecast;

/// <summary>Um item da resposta da IA (spec 6.4). Todos os campos são tolerantes: o guardrail decide o que vale.</summary>
public sealed class AiRecommendationItem
{
    public string? ProdutoId { get; set; }
    public string? Acao { get; set; }
    public string? Prioridade { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? DescontoPct { get; set; }

    /// <summary>decimal para tolerar "14.0" vindo do modelo.</summary>
    public decimal? DiasPromocao { get; set; }
    public string? Motivo { get; set; }
}
