using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Interfaces.UseCases.Base;

namespace Nextflow.Application.UseCases.Base;

public abstract class CreateUseCaseBase<TEntity, TRepository, TRequest, TResponse>(TRepository repository)
    : ICreateUseCase<TRequest, TResponse>
    where TEntity : class
    where TRepository : IBaseRepository<TEntity>
    where TRequest : BaseDto
{
    protected readonly TRepository _repository = repository;

    public virtual async Task<TResponse> Execute(TRequest dto, CancellationToken ct)
    {
        if (dto is null)
            throw new BadRequestException("O corpo da requisição é obrigatório.");

        dto.Validate();

        await ValidateBusinessRules(dto, ct);

        var entity = MapToEntity(dto);

        await BeforePersistence(entity, dto, ct);

        await _repository.AddAsync(entity, ct);
        await AfterPersistence(entity, dto, ct);

        return MapToResponseDto(entity);
    }
    protected abstract TEntity MapToEntity(TRequest dto);
    protected abstract TResponse MapToResponseDto(TEntity entity);
    protected virtual Task ValidateBusinessRules(TRequest dto, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task BeforePersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task AfterPersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
}