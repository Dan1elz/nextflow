namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Gráfico do painel (T-DF1): vendas dos últimos 12 meses (o corrente incluso) × os mesmos meses do ano anterior.</summary>
public class SalesChartDto
{
    public List<SalesChartMonthDto> Months { get; set; } = [];
}
