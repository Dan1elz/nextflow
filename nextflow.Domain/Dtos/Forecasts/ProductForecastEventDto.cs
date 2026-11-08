namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Evento do calendário nos próximos 12 meses (T-DF3); os ligados ao produto vêm marcados.</summary>
public class ProductForecastEventDto
{
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public DateOnly DemandStartsOn { get; set; }
    public bool LinkedToProduct { get; set; }
}
