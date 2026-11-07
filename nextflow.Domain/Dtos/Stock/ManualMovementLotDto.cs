namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Lote novo criado na entrada manual (contrato T9). Se o código já existe na variação com a mesma validade, reaproveita o lote.</summary>
public class ManualMovementLotDto
{
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
}
