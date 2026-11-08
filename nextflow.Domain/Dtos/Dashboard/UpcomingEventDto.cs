namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Bloco "Próximos eventos" (T-DF1): data do evento, início da subida da venda (data − antecedência) e produtos ligados.</summary>
public class UpcomingEventDto
{
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly NextDate { get; set; }
    public DateOnly DemandStartsOn { get; set; }
    public int ProductCount { get; set; }
}
