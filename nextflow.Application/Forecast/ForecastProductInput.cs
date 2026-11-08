using Nextflow.Domain.Enums;

namespace Nextflow.Application.Forecast;

/// <summary>
/// Entrada do cálculo para um produto, montada pelo serviço a partir dos dados derivados (F3.1), do livro-razão e dos pedidos de
/// compra. O estoque é da empresa inteira, somando variações, locais e lotes (RN-CE14: só lido do livro-razão).
/// </summary>
public sealed class ForecastProductInput
{
    public required Guid ProductId { get; init; }
    public required string Name { get; init; }
    public string? Brand { get; init; }
    public SaleUnitEnum SaleUnit { get; init; } = SaleUnitEnum.Unit;

    /// <summary>Casas decimais da unidade de venda: decide quando a demanda prevista arredonda para zero (F3.7, "Sem ação").</summary>
    public int DecimalPlaces { get; init; }

    /// <summary>Categoria de referência (a primeira ativa em ordem alfabética), para o índice sazonal da categoria.</summary>
    public string? ReferenceCategory { get; init; }

    /// <summary>Produto fora de linha nunca recebe Comprar (RN-CE19).</summary>
    public bool IsDiscontinued { get; init; }

    public decimal CurrentStock { get; init; }

    /// <summary>Quantidade em pedidos de compra em Orçamento ou Pendente.</summary>
    public decimal IncomingStock { get; init; }

    /// <summary>Soma dos mínimos efetivos das variações nos locais vendáveis (RN-CE33).</summary>
    public decimal MinStock { get; init; }

    public int LeadTimeDays { get; init; }
    public LeadTimeSourceEnum LeadTimeSource { get; init; } = LeadTimeSourceEnum.Supplier;

    /// <summary>Preço e custo médios ponderados pela venda das variações; custo nulo = sem custo conhecido (desconto máximo de 30%).</summary>
    public decimal Price { get; init; }
    public decimal? CostPrice { get; init; }

    /// <summary>Vencimento mais próximo entre os lotes com saldo e a quantidade desse vencimento (RN-DF07).</summary>
    public DateOnly? NextExpiry { get; init; }
    public decimal NextExpiryQuantity { get; init; }

    /// <summary>Vida útil do perecível: a compra não passa do que vende antes de vencer (RN-CE28).</summary>
    public int? ShelfLifeDays { get; init; }

    /// <summary>Cobertura alvo efetiva (exceção do produto ou política da classe ABC, RN-CE29) e dias de segurança da classe.</summary>
    public int? TargetCoverageDays { get; init; }
    public int? SafetyDays { get; init; }
    public AbcClassEnum? AbcClass { get; init; }

    /// <summary>Pedido mínimo e múltiplo do fornecedor preferido (RN-CE27, RN-CE30).</summary>
    public decimal? MinOrderQty { get; init; }
    public decimal? OrderMultiple { get; init; }

    /// <summary>Desconto (%) da promoção vigente, se houver.</summary>
    public decimal? ActivePromotionDiscountPercent { get; init; }
    public DateOnly? ActivePromotionEndDate { get; init; }

    /// <summary>
    /// Vendas mensais (quantidade) dos últimos <see cref="ForecastCalculationContext.HistoryWindowMonths"/> meses completos, do
    /// mais antigo ao mais recente (último = mês anterior ao corrente), vindas de <c>product_sales_monthly</c>. Mês sem venda = 0.
    /// </summary>
    public required IReadOnlyList<decimal> MonthlySales { get; init; }

    /// <summary>Meses desde a primeira venda (máx. 24).</summary>
    public int MonthsOfHistory { get; init; }

    /// <summary>Índice sazonal do produto (12 valores, janeiro primeiro) de <c>product_seasonality</c>; nulo com menos de 12 meses (RN-DF03).</summary>
    public IReadOnlyList<decimal>? ProductSeasonalIndex { get; init; }

    /// <summary>Índice sazonal da categoria de referência, calculado somando as vendas dos produtos dela (RN-DF03).</summary>
    public IReadOnlyList<decimal>? CategorySeasonalIndex { get; init; }

    /// <summary>Faturamento dos últimos 12 meses (prioriza o envio à IA e define a classe ABC).</summary>
    public decimal RevenueLast12Months { get; init; }

    /// <summary>Nomes dos eventos do calendário ligados ao produto (T-DF3), para o payload da IA.</summary>
    public IReadOnlyList<string> LinkedEvents { get; init; } = [];
}
