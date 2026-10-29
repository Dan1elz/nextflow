using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Base para casos de uso de reativação. Aplicável apenas a entidades <see cref="IDeletable"/>.
/// Retorna 400 se a entidade já estiver ativa ou não suportar reativação.
/// </summary>
public abstract class ReactivateUseCaseBase<TEntity, TRepository>(
    TRepository repository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : IReactivateUseCase<TEntity>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IUnitOfWork _unitOfWork = unitOfWork;
    protected readonly IResultFactory<bool> _resultFactory = resultFactory;

    public virtual async Task<ResultDto<bool>> Execute(Guid id, Guid? userId = null, CancellationToken ct = default)
    {
        // Rastreada: o commit grava só as colunas alteradas (ver IBaseRepository.GetByIdForUpdate).
        var entity = await _repository.GetByIdForUpdate(id, GetInclude(), ct);

        if (entity == null)
            return _resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        if (entity is not IDeletable deletable)
            return _resultFactory.Failure("O recurso não suporta inativação/reativação.", 400);

        if (deletable.IsActive)
            return _resultFactory.Failure($"{Capitalize(entity.Singular)} já está ativ{entity.Preposition}.", 400);

        var businessRuleError = await ValidateBusinessRules(entity, userId, ct);
        if (businessRuleError != null && !businessRuleError.IsSuccess)
            return businessRuleError;

        deletable.Reactivate();
        await BeforePersistence(entity, userId, ct);

        await _unitOfWork.CommitAsync(ct);

        await AfterPersistence(entity, userId, ct);

        return _resultFactory.Success(true);
    }

    /// <summary>Define o que é carregado junto antes da reativação.</summary>
    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    /// <summary>Regras antes de reativar. Retorne <c>null</c> para indicar sucesso.</summary>
    protected virtual Task<ResultDto<bool>?> ValidateBusinessRules(TEntity entity, Guid? userId, CancellationToken ct) => Task.FromResult<ResultDto<bool>?>(null);

    /// <summary>Executado antes do commit. Use para ajustar entidades relacionadas.</summary>
    protected virtual Task BeforePersistence(TEntity entity, Guid? userId, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Executado após o commit. Use para efeitos externos.</summary>
    protected virtual Task AfterPersistence(TEntity entity, Guid? userId, CancellationToken ct) => Task.CompletedTask;

    protected static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
