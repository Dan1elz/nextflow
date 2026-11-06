namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Edição de uma linha da política (T13): cobertura alvo e dias de segurança, inteiros ≥ 0 (RN-CE29).</summary>
public class StockPolicyRequestDto
{
    public int TargetCoverageDays { get; set; }
    public int SafetyDays { get; set; }
}
