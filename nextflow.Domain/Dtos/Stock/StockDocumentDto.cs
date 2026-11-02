using Nextflow.Domain.Enums;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>
/// Documento que movimenta o estoque, entregue ao <c>IStockLedger</c> por quem o originou (venda, compra, transferência,
/// inventário ou ajuste manual). Não vem do corpo da requisição: o use case chamador monta.
/// </summary>
public class StockDocumentDto
{
    public SourceTypeEnum SourceType { get; set; }

    /// <summary>Id do documento de origem; nulo no ajuste manual, que não tem documento próprio.</summary>
    public Guid? SourceId { get; set; }

    public Guid UserId { get; set; }
    public string? Note { get; set; }
}
