using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories.Base;
using Nextflow.Domain.Interfaces.UseCases.Base;
using Nextflow.Domain.Models.Base;
using Nextflow.Domain.Interfaces.Models;

namespace Nextflow.Application.UseCases.Base;

public abstract class DeleteUseCaseBase<TEntity, TRepository>(TRepository repository)
    : IDeleteUseCase<TEntity>
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

        if (entity is IDeletable deletable)
        {
            if (!deletable.IsActive)
                throw new BadRequestException($"{entity.Singular} já está inativ{entity.Preposition}/cancelad{entity.Preposition}.");

            ValidateBusinessRules(entity);
            deletable.Delete();
            await PerformSideEffects(entity, ct, userId);
            await _repository.UpdateAsync(entity, ct);
        }
        else
        {
            ValidateBusinessRules(entity);
            await PerformSideEffects(entity, ct, userId);
            await _repository.RemoveAsync(entity, ct);
        }
    }

    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    protected virtual void ValidateBusinessRules(TEntity entity) { }

    protected virtual Task PerformSideEffects(TEntity entity, CancellationToken ct, Guid? userId = null)
        => Task.CompletedTask;
}