namespace Nextflow.Domain.Dtos.Dashboard;

/// <summary>Venda paga para os KPIs e o gráfico do painel (T-DF1): instante do pagamento, total e custo médio congelado dos itens.</summary>
public record PaidOrderTotalDto(DateTime PaidAt, decimal Total, decimal Cost);
