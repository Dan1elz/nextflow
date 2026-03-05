using Nextflow.Domain.Dtos.Base;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Models;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models.Base;

namespace Nextflow.Application.UseCases.Base;

public abstract class UpdateUseCaseBase<TEntity, TRepository, TRequest, TResponse>(TRepository repository)
    : IUpdateUseCase<TRequest, TResponse>
    where TEntity : BaseModel, IUpdatable<TRequest>
    where TRepository : IBaseRepository<TEntity>
    where TRequest : BaseDto
{
    protected readonly TRepository _repository = repository;

    public virtual async Task<TResponse> Execute(Guid id, TRequest dto, CancellationToken ct)
    {
        dto.Validate();

        var includeExpression = GetInclude();

        var entity = await _repository.GetByIdAsync(id, ct, includeExpression);

        if (entity == null)
            throw new NotFoundException($"{typeof(TEntity).Name} com id {id} não encontrado.");

        if (entity is IDeletable deletable && !deletable.IsActive)
            throw new BadRequestException($"{typeof(TEntity).Name} está inativo/cancelado e não pode ser editado.");

        await ValidateBusinessRules(entity, dto, ct);

        entity.Update(dto);

        await BeforePersistence(entity, dto, ct);

        await _repository.UpdateAsync(entity, ct);

        await AfterPersistence(entity, dto, ct);

        return MapToResponseDto(entity);
    }

    protected abstract TResponse MapToResponseDto(TEntity entity);

    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;
    protected virtual Task ValidateBusinessRules(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task BeforePersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
    protected virtual Task AfterPersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
}