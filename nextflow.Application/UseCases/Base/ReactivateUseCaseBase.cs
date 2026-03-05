using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Interfaces.Models;

namespace Nextflow.Application.UseCases.Base;

public abstract class ReactivateUseCaseBase<TEntity, TRepository>(TRepository repository)
    : IReactivateUseCase<TEntity>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    public virtual async Task Execute(Guid id, CancellationToken ct, Guid? userId = null)
    {
        var includeExpression = GetInclude();

        var entity = await _repository.GetByIdAsync(id, ct, includeExpression);

        if (entity == null)
            throw new NotFoundException($"{entity?.Singular} com id {id} não encontrad{entity?.Preposition}.");

        if (entity is not IDeletable deletable)
            throw new BadRequestException($"{entity.Singular} não suporta reativação.");

        if (deletable.IsActive)
            throw new BadRequestException($"{entity.Singular} já está ativ{entity.Preposition}.");

        ValidateBusinessRules(entity);

        deletable.Reactivate();

        await PerformSideEffects(entity, ct, userId);

        await _repository.UpdateAsync(entity, ct);
    }

    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    protected virtual void ValidateBusinessRules(TEntity entity) { }

    protected virtual Task PerformSideEffects(TEntity entity, CancellationToken ct, Guid? userId = null)
        => Task.CompletedTask;
}