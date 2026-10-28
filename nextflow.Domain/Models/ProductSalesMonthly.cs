using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Domain.Models;

/// <summary>
/// Agregado mensal de vendas confirmadas por produto (cache de cálculo, reconstruído a cada geração de previsão).
/// </summary>
[Table("product_sales_monthly")]
public class ProductSalesMonthly : BaseModel
{
    [ForeignKey("products"), Required(ErrorMessage = "O Produto é obrigatório.")]
    public Guid ProductId { get; private set; }
    public virtual Product? Product { get; private set; }

    public int Year { get; private set; }

    [Range(1, 12, ErrorMessage = "O mês deve estar entre 1 e 12.")]
    public int Month { get; private set; }

    public decimal QuantitySold { get; private set; }

    /// <summary>Σ UnitPrice × Quantity − Discount.</summary>
    public decimal Revenue { get; private set; }

    public int OrdersCount { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public override string Preposition => "a";
    public override string Singular => "venda mensal do produto";
    public override string Plural => "vendas mensais dos produtos";

    private ProductSalesMonthly() : base() { }

    public ProductSalesMonthly(Guid productId, int year, int month, decimal quantitySold, decimal revenue, int ordersCount) : base()
    {
        ProductId = productId;
        Year = year;
        Month = month;
        QuantitySold = quantitySold;
        Revenue = revenue;
        OrdersCount = ordersCount;
        UpdatedAt = DateTime.UtcNow;
    }
}
