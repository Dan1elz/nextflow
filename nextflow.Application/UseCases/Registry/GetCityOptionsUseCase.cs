using System.Linq.Expressions;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

public class GetCityOptionsUseCase(
    ICityRepository repository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<City, ICityRepository, Option>(repository, resultFactory)
{
    protected override Expression<Func<City, bool>> BuildSearch(string term)
        => c => c.Name.ToLower().Contains(term);

    protected override Func<IQueryable<City>, IOrderedQueryable<City>> GetOrderBy() => q => q.OrderBy(c => c.Name).ThenBy(c => c.Id);

    protected override Option MapToOption(City entity) => new(entity.Id.ToString(), $"{entity.Name}/{entity.State.Acronym}");

    /// <summary><c>stateId</c> restringe ao estado; <c>ibgeCode</c> localiza a cidade que o ViaCEP devolveu.</summary>
    protected override void ApplyFilters(FilterExpressionBuilder<City> builder, FilterSet filters)
    {
        builder.WhereGuidEquals(filters, "stateId", c => c.StateId);

        if (filters.TryGetString("ibgeCode", out var ibgeCode))
            builder.And(c => c.IbgeCode == ibgeCode);
    }

    protected override Func<IQueryable<City>, IQueryable<City>> GetInclude() => q => q.Include(c => c.State);
}
