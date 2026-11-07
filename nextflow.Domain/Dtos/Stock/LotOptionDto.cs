namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Lote para escolher no ajuste manual (T9, F1.7): os da variação, com o saldo no local pedido.</summary>
public class LotOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
    public decimal Quantity { get; set; }
}
