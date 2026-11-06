namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Aviso de falta (T-VD1a): onde a variação tem disponível na empresa, mesma loja primeiro.</summary>
public class StockAvailabilityDto
{
    public Guid VariantId { get; set; }
    public List<StockAvailabilityLocationDto> ByLocation { get; set; } = [];
}
