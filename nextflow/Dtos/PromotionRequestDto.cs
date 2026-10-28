namespace Nextflow.Dtos;

/// <summary>
/// Corpo do POST /api/promotions. Contém apenas os campos que o cliente pode enviar;
/// origem (Manual), recomendação e usuário criador são definidos pelo servidor.
/// As validações ficam em <c>CreatePromotionDto</c> (camada de domínio).
/// </summary>
public class PromotionRequestDto
{
    public Guid ProductId { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
}
