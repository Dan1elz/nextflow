using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>
/// Política de estoque de uma classe ABC (T13, RN-CE29): cobertura alvo e dias de segurança. Uma linha por classe, criada
/// pelo seed; o produto com <see cref="Product.TargetCoverageDays"/> preenchido é a exceção à política da classe.
/// </summary>
public class StockPolicy : BaseModel, IUpdatable<StockPolicyRequestDto>
{
    public AbcClassEnum AbcClass { get; private set; }
    public int TargetCoverageDays { get; private set; }
    public int SafetyDays { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "política de estoque";
    public override string Plural => "políticas de estoque";

    private StockPolicy() { }

    public StockPolicy(AbcClassEnum abcClass, StockPolicyRequestDto dto)
    {
        AbcClass = abcClass;
        TargetCoverageDays = dto.TargetCoverageDays;
        SafetyDays = dto.SafetyDays;
    }

    public void Update(StockPolicyRequestDto dto)
    {
        TargetCoverageDays = dto.TargetCoverageDays;
        SafetyDays = dto.SafetyDays;
        UpdatedAt = DateTime.UtcNow;
    }
}
