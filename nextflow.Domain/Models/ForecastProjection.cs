using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

/// <summary>Demanda projetada mês a mês para um produto em uma análise (usada nos gráficos).</summary>
[Table("forecast_projections")]
public class ForecastProjection : BaseModel
{
    [ForeignKey("forecast_runs"), Required(ErrorMessage = "A análise de previsão é obrigatória.")]
    public Guid ForecastRunId { get; private set; }
    public virtual ForecastRun? ForecastRun { get; private set; }

    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    public int Year { get; private set; }

    [Range(1, 12, ErrorMessage = "O mês deve estar entre 1 e 12.")]
    public int Month { get; private set; }

    public decimal ProjectedQuantity { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "projeção de demanda";
    public override string Plural => "projeções de demanda";

    private ForecastProjection() : base() { }

    public ForecastProjection(Guid forecastRunId, Guid productId, int year, int month, decimal projectedQuantity) : base()
    {
        ForecastRunId = forecastRunId;
        ProductId = productId;
        Year = year;
        Month = month;
        ProjectedQuantity = projectedQuantity;
    }
}
