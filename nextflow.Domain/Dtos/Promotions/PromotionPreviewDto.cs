namespace Nextflow.Domain.Dtos.Promotions;

/// <summary>Prévia da promoção (T-VD7), por variação do escopo.</summary>
public class PromotionPreviewDto
{
    public List<PromotionPreviewVariantDto> Variants { get; set; } = [];
}
