namespace Nextflow.Domain.Dtos.Promotions;

/// <summary>Nova promoção manual (T-VD7). Sem <see cref="VariantId"/>, vale para todas as variações do produto.</summary>
public class PromotionRequestDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    /// <summary>Reenvio depois do aviso de preço abaixo do custo (RN-VD13).</summary>
    public bool ConfirmWarnings { get; set; }
}
