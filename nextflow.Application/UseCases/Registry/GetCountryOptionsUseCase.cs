using System.Linq.Expressions;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

public class GetCountryOptionsUseCase(
    ICountryRepository repository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<Country, ICountryRepository, Option>(repository, resultFactory)
{
    protected override Expression<Func<Country, bool>> BuildSearch(string term)
        => c => c.Name.ToLower().Contains(term) || c.AcronymIso.ToLower() == term;

    protected override Func<IQueryable<Country>, IOrderedQueryable<Country>> GetOrderBy() => q => q.OrderBy(c => c.Name);

    protected override Option MapToOption(Country entity) => new(entity.Id.ToString(), entity.Name);
}
