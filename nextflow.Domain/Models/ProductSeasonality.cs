using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Domain.Models;

/// <summary>Índice sazonal de um produto num mês do calendário (RN-DF03), só para produto com ao menos 12 meses de histórico.</summary>
public class ProductSeasonality : BaseModel
{
    public Guid ProductId { get; private set; }
    public short Month { get; private set; }
    public decimal SeasonalIndex { get; private set; }
    public short MonthsOfHistory { get; private set; }

    public virtual Product Product { get; private set; } = null!;

    public override string Preposition => "o";
    public override string Singular => "índice sazonal";
    public override string Plural => "índices sazonais";

    private ProductSeasonality() { }

    public ProductSeasonality(ProductSeasonalityDto dto)
    {
        ProductId = dto.ProductId;
        Month = dto.Month;
        SeasonalIndex = dto.SeasonalIndex;
        MonthsOfHistory = dto.MonthsOfHistory;
    }
}
