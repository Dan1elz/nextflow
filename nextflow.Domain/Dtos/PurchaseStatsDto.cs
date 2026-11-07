namespace Nextflow.Domain.Dtos;

/// <summary>
/// Resumo das compras de um cliente (vendas pagas) ou de um fornecedor (pedidos enviados ou recebidos) para as listagens
/// de cadastro. <see cref="AverageRealLeadTimeDays"/> só vale para fornecedor: média do prazo real dos recebidos.
/// </summary>
public record PurchaseStatsDto(int Count, decimal Total, DateTime? LastAt, double? AverageRealLeadTimeDays = null);
