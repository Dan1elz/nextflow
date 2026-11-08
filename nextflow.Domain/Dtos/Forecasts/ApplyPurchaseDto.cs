namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Aplicar "Comprar" (T-DF4): um grupo por fornecedor; cada grupo vira um pedido de compra em Orçamento (RN-DF15).</summary>
public class ApplyPurchaseDto
{
    public List<ApplyPurchaseGroupDto> Groups { get; set; } = [];
}
