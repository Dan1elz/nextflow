using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Forecast;

/// <summary>
/// Tabela da T-DF2 de uma análise, uma linha por produto. Ordem padrão: ações primeiro (Manter por último), prioridade, produto.
/// Filtros: <c>action</c>, <c>priority</c>, <c>status</c>, <c>confidence</c>, <c>aiAdjusted</c>, <c>abcClass</c>, <c>categoryId</c>
/// (categoria ou subcategoria), <c>supplierId</c> (fornecedor preferido), <c>eventId</c> (produto ligado ao evento) e
/// <c>search</c> (nome do produto).
/// </summary>
public class GetForecastRecommendationsUseCase(
    IForecastRecommendationRepository recommendationRepository,
    IForecastRunRepository runRepository,
    IFileStorage fileStorage,
    IResultFactory<PaginationDto<ForecastRecommendationListItemDto>> resultFactory)
    : GetAllUseCaseBase<ForecastRecommendation, IForecastRecommendationRepository, ForecastRecommendationListItemDto>(recommendationRepository, resultFactory),
        IGetForecastRecommendationsUseCase
{
    private Guid _runId;

    public async Task<ResultDto<PaginationDto<ForecastRecommendationListItemDto>>> Execute(
        Guid runId, int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        if (!await runRepository.Exists(r => r.Id == runId, ct))
            return _resultFactory.Failure(ForecastRunReading.NotFound, 404);

        _runId = runId;
        return await Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<ForecastRecommendation, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var runId = _runId;
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<ForecastRecommendation>()
            .And(r => r.ForecastRunId == runId)
            .WhereEnumEquals(set, "action", r => r.Action)
            .WhereEnumEquals(set, "priority", r => r.Priority)
            .WhereEnumEquals(set, "status", r => r.Status)
            .WhereEnumEquals(set, "confidence", r => r.Confidence)
            .WhereBoolEquals(set, "aiAdjusted", r => r.AiAdjusted)
            .WhereStringContains(set, "search", r => r.Product.Name);

        if (set.TryGetString("abcClass", out var rawClass) && FilterValueParsers.TryParseEnum<AbcClassEnum>(rawClass, out var abcClass) && Enum.IsDefined(abcClass))
            builder.And(r => r.Product.AbcClass == abcClass);
        if (set.TryGetString("categoryId", out var rawCategory) && FilterValueParsers.TryParseGuid(rawCategory, out var categoryId))
            builder.And(r => r.Product.Categories.Any(link => link.CategoryId == categoryId || link.Category.ParentId == categoryId));
        if (set.TryGetString("supplierId", out var rawSupplier) && FilterValueParsers.TryParseGuid(rawSupplier, out var supplierId))
            builder.And(r => r.Product.Suppliers.Any(link => link.IsPreferred && link.SupplierId == supplierId));
        if (set.TryGetString("eventId", out var rawEvent) && FilterValueParsers.TryParseGuid(rawEvent, out var eventId))
            builder.And(r => r.Product.Events.Any(link => link.EventId == eventId));

        return builder.Predicate;
    }

    protected override Func<IQueryable<ForecastRecommendation>, IQueryable<ForecastRecommendation>> GetInclude()
        => q => q.Include(r => r.Product).ThenInclude(p => p.Categories).ThenInclude(link => link.Category).ThenInclude(c => c.Parent)
            .Include(r => r.Product).ThenInclude(p => p.Suppliers).ThenInclude(link => link.Supplier)
            .Include(r => r.AppliedPurchaseOrder);

    protected override Func<IQueryable<ForecastRecommendation>, IOrderedQueryable<ForecastRecommendation>> GetOrderBy()
        => q => q.OrderBy(r => r.Action == RecommendationActionEnum.Hold ? 1 : 0)
            .ThenByDescending(r => r.Priority)
            .ThenBy(r => r.Product.Name)
            .ThenBy(r => r.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<ForecastRecommendation, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<ForecastRecommendation, object?>>>
        {
            ["productName"] = r => r.Product.Name,
            ["priority"] = r => r.Priority,
            ["deadline"] = r => r.Deadline,
            ["coverageDays"] = r => r.CoverageDays,
            ["projectedDemand"] = r => r.ProjectedDemand,
            ["suggestedQuantity"] = r => r.SuggestedQuantity,
        };

    protected override ForecastRecommendationListItemDto MapToResponseDto(ForecastRecommendation entity)
        => new(entity, fileStorage.GetPublicUrl(entity.Product?.ImageKey));
}
