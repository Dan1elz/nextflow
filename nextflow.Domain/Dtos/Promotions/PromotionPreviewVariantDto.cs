namespace Nextflow.Domain.Dtos.Promotions;

/// <summary>Linha da prévia (T-VD7): preço de tabela, promocional, custo médio e margem resultante de uma variação.</summary>
public class PromotionPreviewVariantDto
{
    public Guid VariantId { get; set; }
    /// <summary>Nulo = variação padrão oculta.</summary>
    public string? VariantLabel { get; set; }
    public decimal ListPrice { get; set; }
    public decimal PromoPrice { get; set; }
    public decimal? AverageCost { get; set; }
    public decimal? MarginPercent { get; set; }
    /// <summary>Preço promocional abaixo do custo médio (RN-VD13). Sem custo médio, falso.</summary>
    public bool BelowCost { get; set; }
}
