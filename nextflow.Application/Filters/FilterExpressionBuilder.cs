using System.Linq.Expressions;
using Nextflow.Application.Utils;

namespace Nextflow.Application.Filters;

public sealed class FilterExpressionBuilder<TEntity>
{
    private Expression<Func<TEntity, bool>> _expr = _ => true;
    public bool HasAny { get; private set; }

    public Expression<Func<TEntity, bool>> Predicate => _expr;

    public FilterExpressionBuilder<TEntity> And(Expression<Func<TEntity, bool>> clause)
    {
        _expr = _expr.AndAlso(clause);
        HasAny = true;
        return this;
    }

    public FilterExpressionBuilder<TEntity> WhereGuidEquals(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, Guid>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseGuid(raw, out var guid)) return this;

        return AndEquals(selector, guid);
    }

    public FilterExpressionBuilder<TEntity> WhereGuidEquals(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, Guid?>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseGuid(raw, out var guid)) return this;

        var p = Expression.Parameter(typeof(TEntity), "e");
        var body = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
        var notNull = Expression.NotEqual(body, Expression.Constant(null, typeof(Guid?)));
        var equals = Expression.Equal(body, Expression.Constant((Guid?)guid, typeof(Guid?)));
        return And(Expression.Lambda<Func<TEntity, bool>>(Expression.AndAlso(notNull, equals), p));
    }

    public FilterExpressionBuilder<TEntity> WhereBoolEquals(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, bool>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseBool(raw, out var b)) return this;

        return AndEquals(selector, b);
    }

    public FilterExpressionBuilder<TEntity> WhereIntEquals(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, int>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseInt(raw, out var i)) return this;

        return AndEquals(selector, i);
    }

    public FilterExpressionBuilder<TEntity> WhereIntGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, int>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseInt(raw, out var i)) return this;
        return AndCompare(selector, ExpressionType.GreaterThanOrEqual, i);
    }

    public FilterExpressionBuilder<TEntity> WhereIntLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, int>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseInt(raw, out var i)) return this;
        return AndCompare(selector, ExpressionType.LessThanOrEqual, i);
    }

    public FilterExpressionBuilder<TEntity> WhereDecimalEquals(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, decimal>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDecimal(raw, out var d)) return this;

        return AndEquals(selector, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDecimalGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, decimal>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDecimal(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.GreaterThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDecimalLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, decimal>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDecimal(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.LessThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDateOnlyGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateOnly>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateOnly(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.GreaterThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDateOnlyLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateOnly>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateOnly(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.LessThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDateTimeGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateTime>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateTime(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.GreaterThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereDateTimeLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateTime>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateTime(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.LessThanOrEqual, d);
    }

    public FilterExpressionBuilder<TEntity> WhereStringContains(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, string>> selector,
        bool ignoreCase = true)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        var value = raw.Trim();
        if (value.Length == 0) return this;

        var p = Expression.Parameter(typeof(TEntity), "e");
        var body = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!; // string

        Expression haystack = body;
        Expression needle = Expression.Constant(ignoreCase ? value.ToLowerInvariant() : value);

        if (ignoreCase)
        {
            var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
            haystack = Expression.Call(haystack, toLower);
        }

        var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        var containsCall = Expression.Call(haystack, contains, needle);

        return And(Expression.Lambda<Func<TEntity, bool>>(containsCall, p));
    }

    public FilterExpressionBuilder<TEntity> WhereStringContainsAny(
        FilterSet filters,
        string key,
        params Expression<Func<TEntity, string>>[] selectors)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        var valueLower = raw.Trim().ToLowerInvariant();
        if (valueLower.Length == 0) return this;

        var p = Expression.Parameter(typeof(TEntity), "e");
        Expression? orBody = null;

        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        var contains = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;
        var needle = Expression.Constant(valueLower);

        foreach (var selector in selectors)
        {
            var selBody = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
            var lowered = Expression.Call(selBody, toLower);
            var containsCall = Expression.Call(lowered, contains, needle);
            orBody = orBody == null ? containsCall : Expression.OrElse(orBody, containsCall);
        }

        if (orBody == null) return this;
        return And(Expression.Lambda<Func<TEntity, bool>>(orBody, p));
    }

    public FilterExpressionBuilder<TEntity> WhereDoubleGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, double>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDecimal(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.GreaterThanOrEqual, (double)d);
    }

    public FilterExpressionBuilder<TEntity> WhereDoubleLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, double>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDecimal(raw, out var d)) return this;
        return AndCompare(selector, ExpressionType.LessThanOrEqual, (double)d);
    }

    public FilterExpressionBuilder<TEntity> WhereEnumEquals<TEnum>(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, TEnum>> selector) where TEnum : struct, Enum
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseInt(raw, out var i)) return this;
        if (!Enum.IsDefined(typeof(TEnum), i)) return this;
        var enumVal = (TEnum)Enum.ToObject(typeof(TEnum), i);
        return AndEquals(selector, enumVal);
    }

    public FilterExpressionBuilder<TEntity> WhereNullableDateOnlyLte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateOnly?>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateOnly(raw, out var d)) return this;

        var p = Expression.Parameter(typeof(TEntity), "e");
        var body = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
        var hasValue = Expression.Property(body, "HasValue");
        var value = Expression.Property(body, "Value");
        var compare = Expression.LessThanOrEqual(value, Expression.Constant(d, typeof(DateOnly)));
        var combined = Expression.AndAlso(hasValue, compare);
        return And(Expression.Lambda<Func<TEntity, bool>>(combined, p));
    }

    public FilterExpressionBuilder<TEntity> WhereNullableDateOnlyGte(
        FilterSet filters,
        string key,
        Expression<Func<TEntity, DateOnly?>> selector)
    {
        if (!filters.TryGetString(key, out var raw)) return this;
        if (!FilterValueParsers.TryParseDateOnly(raw, out var d)) return this;

        var p = Expression.Parameter(typeof(TEntity), "e");
        var body = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
        var hasValue = Expression.Property(body, "HasValue");
        var value = Expression.Property(body, "Value");
        var compare = Expression.GreaterThanOrEqual(value, Expression.Constant(d, typeof(DateOnly)));
        var combined = Expression.AndAlso(hasValue, compare);
        return And(Expression.Lambda<Func<TEntity, bool>>(combined, p));
    }

    private FilterExpressionBuilder<TEntity> AndEquals<TValue>(
        Expression<Func<TEntity, TValue>> selector,
        TValue value)
    {
        var p = Expression.Parameter(typeof(TEntity), "e");
        var left = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
        var right = Expression.Constant(value, typeof(TValue));
        var equals = Expression.Equal(left, right);
        return And(Expression.Lambda<Func<TEntity, bool>>(equals, p));
    }

    private FilterExpressionBuilder<TEntity> AndCompare<TValue>(
        Expression<Func<TEntity, TValue>> selector,
        ExpressionType comparison,
        TValue value)
    {
        var p = Expression.Parameter(typeof(TEntity), "e");
        var left = new ReplaceParameterVisitor(selector.Parameters[0], p).Visit(selector.Body)!;
        var right = Expression.Constant(value, typeof(TValue));

        var body = comparison switch
        {
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            ExpressionType.GreaterThan => Expression.GreaterThan(left, right),
            ExpressionType.LessThan => Expression.LessThan(left, right),
            _ => throw new NotSupportedException($"Comparison não suportado: {comparison}")
        };

        return And(Expression.Lambda<Func<TEntity, bool>>(body, p));
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}

