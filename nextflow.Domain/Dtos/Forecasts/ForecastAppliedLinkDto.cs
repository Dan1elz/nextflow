namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Documento gerado ao aplicar a recomendação (F3.4): <c>Type</c> "PurchaseOrder" ou "Promotion".</summary>
public class ForecastAppliedLinkDto
{
    public const string PurchaseOrderType = "PurchaseOrder";
    public const string PromotionType = "Promotion";

    public string Type { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public int? Number { get; set; }

    public ForecastAppliedLinkDto() { }

    public ForecastAppliedLinkDto(string type, Guid id, int? number)
    {
        Type = type;
        Id = id;
        Number = number;
    }
}
