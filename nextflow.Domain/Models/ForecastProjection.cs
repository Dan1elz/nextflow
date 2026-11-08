using Nextflow.Domain.Dtos.Forecasts;

namespace Nextflow.Domain.Models;

/// <summary>Demanda projetada de um produto num mês (RN-DF19: depois comparada com o vendido). Por produto, como a recomendação (RN-CE16).</summary>
public class ForecastProjection : BaseModel
{
    public Guid ForecastRunId { get; private set; }
    public Guid ProductId { get; private set; }
    public short Year { get; private set; }
    public short Month { get; private set; }
    public decimal ProjectedQuantity { get; private set; }

    public virtual ForecastRun ForecastRun { get; private set; } = null!;
    public virtual Product Product { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "projeção";
    public override string Plural => "projeções";

    private ForecastProjection() { }

    public ForecastProjection(NewForecastProjectionDto dto)
    {
        ForecastRunId = dto.ForecastRunId;
        ProductId = dto.ProductId;
        Year = dto.Year;
        Month = dto.Month;
        ProjectedQuantity = dto.ProjectedQuantity;
    }
}
