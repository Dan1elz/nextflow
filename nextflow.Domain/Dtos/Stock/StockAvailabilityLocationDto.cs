namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Um local com saldo da variação no aviso de falta (T-VD1a).</summary>
public class StockAvailabilityLocationDto
{
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    /// <summary>Mesma loja do local de venda (o excluído da consulta).</summary>
    public bool SameStore { get; set; }
    public decimal Available { get; set; }
}
