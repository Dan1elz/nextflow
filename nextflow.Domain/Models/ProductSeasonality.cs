using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

/// <summary>
/// Perfil sazonal do produto por mês-calendário (cache; upsert a cada geração de previsão).
/// </summary>
[Table("product_seasonality")]
public class ProductSeasonality : BaseModel
{
    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    [Range(1, 12, ErrorMessage = "O mês deve estar entre 1 e 12.")]
    public int Month { get; private set; }

    /// <summary>1.0 = média; 1.8 = 80% acima da média.</summary>
    public decimal SeasonalIndex { get; private set; }

    public int MonthsOfHistory { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "sazonalidade do produto";
    public override string Plural => "sazonalidades dos produtos";

    private ProductSeasonality() : base() { }

    public ProductSeasonality(Guid productId, int month, decimal seasonalIndex, int monthsOfHistory) : base()
    {
        ProductId = productId;
        Month = month;
        SeasonalIndex = seasonalIndex;
        MonthsOfHistory = monthsOfHistory;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Update(decimal seasonalIndex, int monthsOfHistory)
    {
        SeasonalIndex = seasonalIndex;
        MonthsOfHistory = monthsOfHistory;
        UpdatedAt = DateTime.UtcNow;
    }
}
