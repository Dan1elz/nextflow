namespace Nextflow.Application.Forecast;

/// <summary>Evento do calendário dentro do horizonte (F1.2): vai no payload da IA. <c>DemandStartsOn</c> = data − <c>LeadDays</c> (T-DF3).</summary>
public sealed record ForecastEvent(string Name, DateOnly Date, int LeadDays)
{
    public DateOnly DemandStartsOn => Date.AddDays(-LeadDays);
}
