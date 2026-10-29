using System.Linq.Expressions;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Base para casos de uso de listagem paginada com filtros dinâmicos e ordenação por lista branca (ADR 0010, extensão 3).
/// </summary>
public abstract class GetAllUseCaseBase<TEntity, TRepository, TResponse>(
    TRepository repository,
    IResultFactory<PaginationDto<TResponse>> resultFactory)
    : IGetAllUseCase<TEntity, TResponse>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IResultFactory<PaginationDto<TResponse>> _resultFactory = resultFactory;

    public virtual async Task<ResultDto<PaginationDto<TResponse>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        (offset, limit) = PagingLimits.Normalize(offset, limit);

        var expressionFilter = BuildFilterExpression(filters);
        var includeExpression = GetInclude();
        var orderBy = ResolveOrderBy(sort, order) ?? GetOrderBy();

        var items = await _repository.GetAll(expressionFilter, offset, limit, includeExpression, orderBy, ct);
        var total = await _repository.Count(expressionFilter, ct);

        var pagination = PaginationDto<TResponse>.Paginate(
            items.Select(MapToResponseDto),
            total
        );

        return _resultFactory.Success(pagination);
    }

    /// <summary>Mapeia cada entidade retornada pelo repositório para o DTO de resposta.</summary>
    protected abstract TResponse MapToResponseDto(TEntity entity);

    /// <summary>
    /// Constrói o predicado de filtragem a partir dos filtros da query. Use <c>FilterSet</c> e
    /// <c>FilterExpressionBuilder</c>. Por padrão retorna <c>true</c> (sem filtro).
    /// </summary>
    protected virtual Expression<Func<TEntity, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters) => entity => true;

    /// <summary>Define o que é carregado junto (inclua só o que o DTO de resposta mapeia).</summary>
    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    /// <summary>
    /// Ordenação padrão (ex.: <c>q => q.OrderBy(c => c.Name)</c>). Vale quando a query não traz <c>sort</c> ou traz um
    /// campo fora de <see cref="GetSortableFields"/>. <c>null</c> = <c>CreatedAt</c> descendente (repositório).
    /// </summary>
    protected virtual Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? GetOrderBy() => null;

    /// <summary>
    /// Lista branca de campos aceitos em <c>sort</c> (chave em camelCase, como o front envia). Campo fora da lista é
    /// ignorado: ordenar por qualquer coluna abriria consulta sem índice.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, Expression<Func<TEntity, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<TEntity, object?>>>();

    private Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? ResolveOrderBy(string? sort, string? order)
    {
        if (string.IsNullOrWhiteSpace(sort))
            return null;

        var field = GetSortableFields()
            .FirstOrDefault(f => string.Equals(f.Key, sort.Trim(), StringComparison.OrdinalIgnoreCase))
            .Value;

        if (field is null)
            return null;

        var descending = string.Equals(order?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
        return q => descending
            ? q.OrderByDescending(field).ThenBy(e => e.Id)
            : q.OrderBy(field).ThenBy(e => e.Id);
    }
}
