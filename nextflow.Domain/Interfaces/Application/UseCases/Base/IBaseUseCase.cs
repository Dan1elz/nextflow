using Nextflow.Domain.Dtos;

namespace Nextflow.Domain.Interfaces.Application.UseCases.Base;

public interface ICreateUseCase<TRequest, TResponse>
{
    Task<ResultDto<TResponse>> Execute(TRequest dto, CancellationToken ct = default);
}

public interface IUpdateUseCase<TRequest, TResponse>
{
    Task<ResultDto<TResponse>> Execute(Guid id, TRequest dto, CancellationToken ct = default);
}

public interface IDeleteUseCase<TEntity>
{
    Task<ResultDto<bool>> Execute(Guid id, Guid? userId = null, CancellationToken ct = default);
}

public interface IReactivateUseCase<TEntity>
{
    Task<ResultDto<bool>> Execute(Guid id, Guid? userId = null, CancellationToken ct = default);
}

public interface IGetByIdUseCase<TEntity, TResponse>
{
    Task<ResultDto<TResponse>> Execute(Guid id, CancellationToken ct = default);
}

public interface IGetAllUseCase<TEntity, TResponse>
{
    Task<ResultDto<PaginationDto<TResponse>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default);
}
