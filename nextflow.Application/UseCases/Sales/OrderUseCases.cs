using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;

/// <summary>
/// Listagem de pedidos (T-VD3) da loja selecionada, mais recente primeiro. Filtros: <c>status</c>, <c>channel</c>,
/// <c>paymentMethod</c>, período <c>from</c>/<c>to</c> em dias de negócio, <c>search</c> (número ou nome do cliente),
/// <c>clientId</c>, <c>userId</c> (vendedor), <c>productId</c> (algum item do produto) e total entre <c>minTotal</c> e <c>maxTotal</c>.
/// </summary>
public class GetAllOrdersUseCase(
    IOrderRepository repository,
    IBusinessClock clock,
    IResultFactory<PaginationDto<OrderListItemDto>> resultFactory)
    : GetAllUseCaseBase<Order, IOrderRepository, OrderListItemDto>(repository, resultFactory), IGetOrdersUseCase
{
    private Guid? _storeId;

    public Task<ResultDto<PaginationDto<OrderListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, string? sort, string? order, Guid? storeId,
        CancellationToken ct = default)
    {
        _storeId = storeId;
        return Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<Order, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Order>()
            .WhereEnumEquals(set, "status", o => o.Status)
            .WhereEnumEquals(set, "channel", o => o.Channel)
            .WhereGuidEquals(set, "clientId", o => o.ClientId)
            .WhereGuidEquals(set, "userId", o => o.UserId)
            .WhereDecimalGte(set, "minTotal", o => o.Total)
            .WhereDecimalLte(set, "maxTotal", o => o.Total);

        if (set.TryGetString("productId", out var rawProduct) && FilterValueParsers.TryParseGuid(rawProduct, out var productId))
            builder.And(o => o.Items.Any(i => i.Variant.ProductId == productId));

        if (_storeId is { } storeId)
            builder.And(o => o.StoreId == storeId);

        if (set.TryGetString("paymentMethod", out var rawMethod)
            && Enum.TryParse<PaymentMethodEnum>(rawMethod, true, out var method) && Enum.IsDefined(method))
            builder.And(o => o.Payments.Any(p => p.Method == method));

        if (set.TryGetString("search", out var search))
        {
            var term = search.Trim().ToLowerInvariant();
            if (int.TryParse(term, out var number))
                builder.And(o => o.Number == number);
            else
                builder.And(o => o.Client != null
                    && (o.Client.Name.ToLower().Contains(term) || (o.Client.LastName != null && o.Client.LastName.ToLower().Contains(term))));
        }

        // Período em dias de negócio (America/Sao_Paulo): "to" inclui o dia inteiro.
        if (set.TryGetString("from", out var rawFrom) && FilterValueParsers.TryParseDateOnly(rawFrom, out var from))
        {
            var start = clock.StartOfDayUtc(from);
            builder.And(o => o.CreatedAt >= start);
        }
        if (set.TryGetString("to", out var rawTo) && FilterValueParsers.TryParseDateOnly(rawTo, out var to))
        {
            var end = clock.StartOfDayUtc(to.AddDays(1));
            builder.And(o => o.CreatedAt < end);
        }

        return builder.Predicate;
    }

    protected override Func<IQueryable<Order>, IQueryable<Order>> GetInclude()
        => q => q.Include(o => o.Client).Include(o => o.Store).Include(o => o.SaleLocation).Include(o => o.User)
            .Include(o => o.Items).Include(o => o.Payments);

    protected override Func<IQueryable<Order>, IOrderedQueryable<Order>> GetOrderBy()
        => q => q.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Number);

    protected override IReadOnlyDictionary<string, Expression<Func<Order, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Order, object?>>>
        {
            ["number"] = o => o.Number,
            ["createdAt"] = o => o.CreatedAt,
            ["total"] = o => o.Total,
        };

    protected override OrderListItemDto MapToResponseDto(Order entity) => new(entity);
}

/// <summary>Detalhe da venda (T-VD5): itens, pagamentos, troco e totais como foram gravados.</summary>
public class GetOrderByIdUseCase(IOrderRepository repository, IResultFactory<OrderDetailDto> resultFactory)
    : GetByIdUseCaseBase<Order, IOrderRepository, OrderDetailDto>(repository, resultFactory)
{
    protected override Func<IQueryable<Order>, IQueryable<Order>> GetInclude() => OrderDetails.Include;

    protected override OrderDetailDto MapToResponseDto(Order entity) => new(entity);
}

internal static class OrderDetails
{
    public static IQueryable<Order> Include(IQueryable<Order> query)
        => query.Include(o => o.Store).Include(o => o.SaleLocation).Include(o => o.Client).Include(o => o.User)
            .Include(o => o.Payments)
            .Include(o => o.Items).ThenInclude(i => i.Promotion)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.AttributeValues)
                .ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);
}
