using Nextflow.Application.Filters;
using Nextflow.Application.Forecast;
using Nextflow.Application.Services.Forecast;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>
/// Visão geral da precisão (T-DF10, F3.6c) nos últimos N meses completos (padrão 12, até 36): um item por mês da janela (erro médio
/// dos produtos medidos no mês; nulo sem medição), erro médio dos produtos por classe ABC e por categoria principal e a parcela de
/// produtos dentro de ±15%. "Erro médio" de um grupo = média simples do erro médio (MAPE) de cada produto medido.
/// </summary>
public class GetForecastAccuracySummaryUseCase(
    ForecastAccuracyReader reader,
    IResultFactory<ForecastAccuracySummaryDto> resultFactory) : IGetForecastAccuracySummaryUseCase
{
    public async Task<ResultDto<ForecastAccuracySummaryDto>> Execute(int months, CancellationToken ct = default)
    {
        var window = reader.Window(months);
        var products = (await reader.LoadAsync(window, ct)).Where(p => p.Accuracy.MonthsMeasured > 0).ToList();

        var errorsByMonth = products
            .SelectMany(p => p.Accuracy.Months)
            .Where(m => m.ErrorPercent.HasValue)
            .ToLookup(m => m.Month, m => m.ErrorPercent!.Value);

        return resultFactory.Success(new ForecastAccuracySummaryDto
        {
            Monthly = [.. window.Select(month => new ForecastAccuracyMonthDto
            {
                Month = ForecastAccuracyFormat.Month(month),
                MapePercent = errorsByMonth[month].Any() ? ForecastAccuracyFormat.Round(errorsByMonth[month].Average()) : null,
                ProductsMeasured = errorsByMonth[month].Count(),
            })],
            ByAbcClass = [.. products
                .Where(p => p.Product.AbcClass.HasValue)
                .GroupBy(p => p.Product.AbcClass!.Value)
                .OrderBy(g => g.Key)
                .Select(g => new ForecastAccuracyClassDto { AbcClass = g.Key.GetApiName(), MapePercent = Average(g) })],
            ByCategory = [.. products
                .Select(p => (Path: ForecastAccuracyFormat.MainCategoryPath(p.Product), p))
                .Where(x => x.Path != null)
                .GroupBy(x => x.Path!, x => x.p)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ForecastAccuracyCategoryDto { CategoryPath = g.Key, MapePercent = Average(g) })],
            WithinTolerancePercent = ForecastAccuracyCalculator.WithinTolerancePercent(products.Select(p => p.Accuracy)),
        });
    }

    private static decimal Average(IEnumerable<ProductAccuracyResult> products)
        => ForecastAccuracyFormat.Round(products.Average(p => p.Accuracy.MapePercent!.Value));
}

/// <summary>
/// Tabela de precisão por produto (T-DF10, F3.6c) nos últimos 12 meses completos: só produtos com algum mês medido, pior erro médio
/// primeiro (depois o nome). Filtros: <c>search</c> (nome), <c>abcClass</c> e <c>bias</c> (Under, Over, Balanced).
/// </summary>
public class GetForecastAccuracyProductsUseCase(
    ForecastAccuracyReader reader,
    IResultFactory<PaginationDto<ForecastAccuracyProductDto>> resultFactory) : IGetForecastAccuracyProductsUseCase
{
    private const int MaxLimit = 100;

    public async Task<ResultDto<PaginationDto<ForecastAccuracyProductDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, CancellationToken ct = default)
    {
        var set = new FilterSet(filters);
        var products = (await reader.LoadAsync(reader.Window(ForecastAccuracyReader.DefaultMonths), ct))
            .Where(p => p.Accuracy.MonthsMeasured > 0);

        if (set.TryGetString("search", out var search) && !string.IsNullOrWhiteSpace(search))
            products = products.Where(p => p.Product.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
        if (set.TryGetString("abcClass", out var rawClass) && FilterValueParsers.TryParseEnum<AbcClassEnum>(rawClass, out var abcClass))
            products = products.Where(p => p.Product.AbcClass == abcClass);
        if (set.TryGetString("bias", out var rawBias) && FilterValueParsers.TryParseEnum<ForecastBiasEnum>(rawBias, out var bias))
            products = products.Where(p => p.Accuracy.Bias == bias);

        var ordered = products
            .OrderByDescending(p => p.Accuracy.MapePercent)
            .ThenBy(p => p.Product.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Product.Id)
            .ToList();
        var page = ordered.Skip(Math.Max(offset, 0)).Take(Math.Clamp(limit, 1, MaxLimit)).Select(ToDto);

        return resultFactory.Success(PaginationDto<ForecastAccuracyProductDto>.Paginate(page, ordered.Count));
    }

    private static ForecastAccuracyProductDto ToDto(ProductAccuracyResult result)
    {
        var (product, accuracy) = result;
        var last = accuracy.Months.LastOrDefault(m => m.ErrorPercent.HasValue);
        return new ForecastAccuracyProductDto
        {
            ProductId = product.Id,
            ProductName = product.Name,
            AbcClass = product.AbcClass?.GetApiName(),
            MonthsMeasured = accuracy.MonthsMeasured,
            MapePercent = accuracy.MapePercent!.Value,
            Bias = accuracy.Bias!.Value.GetApiName(),
            LastMonth = last == null ? null : new ForecastAccuracyLastMonthDto
            {
                Month = ForecastAccuracyFormat.Month(last.Month),
                Projected = last.Projected,
                Actual = last.Actual,
            },
        };
    }
}

/// <summary>Formatação comum às rotas de precisão.</summary>
internal static class ForecastAccuracyFormat
{
    public static string Month(YearMonth month) => $"{month.Year:0000}-{month.Month:00}";

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Categoria principal: a mais específica ativa; entre iguais, a primeira pelo nome. "Pai &gt; Filha", como na T-DF2.</summary>
    public static string? MainCategoryPath(Product product)
    {
        var category = product.Categories
            .Where(link => link.Category != null && link.Category.IsActive)
            .Select(link => link.Category)
            .OrderBy(c => c.ParentId == null ? 1 : 0)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (category == null)
            return null;

        return category.Parent != null ? $"{category.Parent.Name} > {category.Name}" : category.Name;
    }
}
