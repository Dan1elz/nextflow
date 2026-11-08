namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Descartar (T-DF8): motivo opcional (<c>DismissReason</c>) e texto; "Other" exige o texto.</summary>
public class DismissRecommendationDto
{
    public string? ReasonCode { get; set; }
    public string? Note { get; set; }
}
