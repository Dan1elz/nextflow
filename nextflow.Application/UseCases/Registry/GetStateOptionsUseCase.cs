using System.Linq.Expressions;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

public class GetStateOptionsUseCase(
    IStateRepository repository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<State, IStateRepository, Option>(repository, resultFactory)
{
    // Os 27 estados cabem no combo inteiro.
    protected override int Limit => 50;

    protected override Expression<Func<State, bool>> BuildSearch(string term)
        => s => s.Name.ToLower().Contains(term) || s.Acronym.ToLower() == term;

    protected override Func<IQueryable<State>, IOrderedQueryable<State>> GetOrderBy() => q => q.OrderBy(s => s.Name);

    protected override Option MapToOption(State entity) => new(entity.Id.ToString(), $"{entity.Name}/{entity.Acronym}");

    protected override void ApplyFilters(FilterExpressionBuilder<State> builder, FilterSet filters)
        => builder.WhereGuidEquals(filters, "countryId", s => s.CountryId);
}
