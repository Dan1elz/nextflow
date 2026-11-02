namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Aba Estoque do T3: matriz variação × local, só locais ativos de lojas ativas (filtrados pela loja, se houver).</summary>
public class StockMatrixDto
{
    public List<StockMatrixLocationDto> Locations { get; set; } = [];
    public List<StockMatrixRowDto> Rows { get; set; } = [];
}
