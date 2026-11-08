using Nextflow.Domain.Dtos.Stock;

namespace Nextflow.Domain.Dtos.Forecasts;

/// <summary>Aplicar "Transferir" (F3.4b): sem itens, transfere o que a recomendação sugeriu; com itens, as quantidades ajustadas.</summary>
public class ApplyTransferDto
{
    public List<StockTransferItemRequestDto>? Items { get; set; }
    public string? Note { get; set; }
}
