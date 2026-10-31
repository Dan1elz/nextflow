using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Categories;

namespace Nextflow.Domain.Interfaces.Application.UseCases;

public interface IGetCategoryTreeUseCase
{
    Task<ResultDto<List<CategoryNodeDto>>> Execute(CancellationToken ct = default);
}
