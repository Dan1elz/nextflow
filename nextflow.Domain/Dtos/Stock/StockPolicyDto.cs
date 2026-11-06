using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Stock;

/// <summary>Linha do T13. <see cref="ProductCount"/> conta os produtos ativos na classe; fica 0 até a análise (F3) classificar.</summary>
public class StockPolicyDto
{
    public Guid Id { get; set; }
    public string AbcClass { get; set; } = string.Empty;
    public int TargetCoverageDays { get; set; }
    public int SafetyDays { get; set; }
    public int ProductCount { get; set; }

    public StockPolicyDto() { }

    public StockPolicyDto(StockPolicy entity, int productCount)
    {
        Id = entity.Id;
        AbcClass = entity.AbcClass.GetApiName();
        TargetCoverageDays = entity.TargetCoverageDays;
        SafetyDays = entity.SafetyDays;
        ProductCount = productCount;
    }
}
