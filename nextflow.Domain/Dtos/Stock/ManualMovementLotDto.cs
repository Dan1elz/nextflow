namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Lote novo criado na entrada manual (contrato T9). Recusado enquanto a F1.7 (lotes) estiver fora.</summary>
public class ManualMovementLotDto
{
    public string Code { get; set; } = string.Empty;
    public DateOnly ExpiresOn { get; set; }
}
