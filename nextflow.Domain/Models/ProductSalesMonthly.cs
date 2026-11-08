using Nextflow.Domain.Dtos.DerivedData;

namespace Nextflow.Domain.Models;

/// <summary>
/// Vendas de um produto num mês de negócio, derivadas das vendas pagas e do saldo diário (F3.1, DER-4). Nunca editada pela
/// API: o recálculo apaga e recria a tabela inteira.
/// </summary>
public class ProductSalesMonthly : BaseModel
{
    public Guid ProductId { get; private set; }
    public short Year { get; private set; }
    public short Month { get; private set; }
    public decimal QuantitySold { get; private set; }
    public decimal QuantitySoldOnPromo { get; private set; }
    public short DaysOutOfStock { get; private set; }
    public decimal LostDemandQty { get; private set; }
    public decimal AvgPrice { get; private set; }
    public decimal Revenue { get; private set; }

    public virtual Product Product { get; private set; } = null!;

    public override string Preposition => "a";
    public override string Singular => "venda mensal do produto";
    public override string Plural => "vendas mensais do produto";

    private ProductSalesMonthly() { }

    public ProductSalesMonthly(ProductSalesMonthlyDto dto)
    {
        ProductId = dto.ProductId;
        Year = dto.Year;
        Month = dto.Month;
        QuantitySold = dto.QuantitySold;
        QuantitySoldOnPromo = dto.QuantitySoldOnPromo;
        DaysOutOfStock = dto.DaysOutOfStock;
        LostDemandQty = dto.LostDemandQty;
        AvgPrice = dto.AvgPrice;
        Revenue = dto.Revenue;
    }
}
