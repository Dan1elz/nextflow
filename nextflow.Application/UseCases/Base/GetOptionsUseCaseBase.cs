using System.Linq.Expressions;
using Nextflow.Application.Filters;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Base dos combos (decisão 3 de 29/10): busca, só ativos (quando a entidade é <see cref="IDeletable"/>) e no máximo
/// <see cref="Limit"/> itens, devolvendo lista.
/// </summary>
public abstract class GetOptionsUseCaseBase<TEntity, TRepository, TOption>(
    TRepository repository,
    IResultFactory<List<TOption>> resultFactory)
    : IGetOptionsUseCase<TEntity, TOption>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IResultFactory<List<TOption>> _resultFactory = resultFactory;

    /// <summary>Máximo de itens devolvidos.</summary>
    protected virtual int Limit => 20;

    public virtual async Task<ResultDto<List<TOption>>> Execute(
        string? search, IReadOnlyDictionary<string, string>? filters = null, CancellationToken ct = default)
    {
        var builder = new FilterExpressionBuilder<TEntity>();

        if (typeof(IDeletable).IsAssignableFrom(typeof(TEntity)))
            builder.And(OnlyActive());

        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
            builder.And(BuildSearch(term));

        ApplyFilters(builder, new FilterSet(filters));

        var items = (await _repository.GetAll(builder.Predicate, 0, Limit, GetInclude(), GetOrderBy(), ct)).ToList();
        await PrepareMapping(items, ct);

        return _resultFactory.Success([.. items.Select(MapToOption)]);
    }

    /// <summary>Predicado da busca. <paramref name="term"/> chega sem espaços nas pontas e em minúsculas.</summary>
    protected abstract Expression<Func<TEntity, bool>> BuildSearch(string term);

    protected abstract Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> GetOrderBy();

    protected abstract TOption MapToOption(TEntity entity);

    /// <summary>Filtros além da busca (ex.: <c>stateId</c> no combo de cidades).</summary>
    protected virtual void ApplyFilters(FilterExpressionBuilder<TEntity> builder, FilterSet filters) { }

    /// <summary>Define o que é carregado junto (inclua só o que a opção mostra).</summary>
    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    /// <summary>Carrega por consulta separada o que a opção mostra e não tem navegação.</summary>
    protected virtual Task PrepareMapping(IReadOnlyList<TEntity> items, CancellationToken ct) => Task.CompletedTask;

    private static Expression<Func<TEntity, bool>> OnlyActive()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        return Expression.Lambda<Func<TEntity, bool>>(Expression.Property(parameter, nameof(IDeletable.IsActive)), parameter);
    }
}
