using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Forecasts;

/// <summary>
/// Apaga e reconstrói <c>product_sales_monthly</c> a partir dos itens de pedidos com pagamento confirmado,
/// nos últimos 36 meses (mês corrente incluído), com o mês calculado no fuso America/Sao_Paulo.
/// A agregação é feita no banco (GROUP BY produto × ano × mês); a troca é atômica (transação).
/// </summary>
public class RebuildProductSalesMonthlyUseCase(
    IProductSalesMonthlyRepository repository,
    IUnitOfWork unitOfWork
) : IRebuildProductSalesMonthlyUseCase
{
    public const int MonthsToKeep = 36;

    private readonly IProductSalesMonthlyRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> Execute(CancellationToken ct)
    {
        var today = BusinessClock.Today;
        var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-(MonthsToKeep - 1));
        var fromUtc = BusinessClock.ToUtc(firstMonth, TimeOnly.MinValue);

        var aggregates = await _repository.AggregateConfirmedSalesAsync(fromUtc, null, ct);
        var rows = aggregates
            .Select(a => new ProductSalesMonthly(a.ProductId, a.Year, a.Month, a.QuantitySold, a.Revenue, a.OrdersCount))
            .ToList();

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await _repository.DeleteAllAsync(token);
            if (rows.Count > 0)
                await _repository.AddRangeAsync(rows, token);
            return rows.Count;
        }, ct);
    }
}
