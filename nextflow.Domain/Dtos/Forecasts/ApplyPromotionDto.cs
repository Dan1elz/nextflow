namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>
/// Aplicar "Promover" (T-DF6). Sem <see cref="VariantIds"/>, a promoção vale para o produto inteiro; com elas, uma promoção por
/// variação. <see cref="ConfirmWarnings"/> reenvia depois do aviso de preço abaixo do custo (RN-VD13).
/// </summary>
public class ApplyPromotionDto
{
    public decimal DiscountPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public List<Guid>? VariantIds { get; set; }
    public bool ConfirmWarnings { get; set; }
}
