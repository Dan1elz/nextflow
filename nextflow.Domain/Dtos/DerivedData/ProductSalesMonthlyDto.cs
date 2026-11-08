namespace Nextflow.Domain.Dtos.DerivedData;

/// <summary>Uma linha de <c>product_sales_monthly</c> calculada pelo recálculo (F3.1): produto, mês de negócio e agregados.</summary>
public record ProductSalesMonthlyDto(
    Guid ProductId,
    short Year,
    short Month,
    decimal QuantitySold,
    decimal QuantitySoldOnPromo,
    short DaysOutOfStock,
    decimal LostDemandQty,
    decimal AvgPrice,
    decimal Revenue);
