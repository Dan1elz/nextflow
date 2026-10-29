using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Base para casos de uso de atualização. Orquestra: busca rastreada → validação de formato
/// → regras de negócio → <see cref="UpdateEntity"/> → commit único → efeitos.
/// </summary>
public abstract class UpdateUseCaseBase<TEntity, TRepository, TRequest, TResponse>(
    TRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<TRequest> validator,
    IResultFactory<TResponse> resultFactory)
    : IUpdateUseCase<TRequest, TResponse>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IUnitOfWork _unitOfWork = unitOfWork;
    protected readonly IValidator<TRequest> _validator = validator;
    protected readonly IResultFactory<TResponse> _resultFactory = resultFactory;

    public virtual async Task<ResultDto<TResponse>> Execute(Guid id, TRequest dto, CancellationToken ct = default)
    {
        if (dto is null)
            return _resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        // Rastreada: o commit grava só as colunas alteradas (ver IBaseRepository.GetByIdForUpdate).
        var entity = await _repository.GetByIdForUpdate(id, GetInclude(), ct);

        if (entity == null)
            return _resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        if (entity is IDeletable deletable && !deletable.IsActive)
            return _resultFactory.Failure($"{Capitalize(entity.Singular)} está inativ{entity.Preposition} e não pode ser alterad{entity.Preposition}.", 400);

        var validationResult = await _validator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
            return _resultFactory.Failure(validationResult, 422);

        var businessRuleError = await ValidateBusinessRules(entity, dto, ct);
        if (businessRuleError != null && !businessRuleError.IsSuccess)
            return businessRuleError;

        UpdateEntity(entity, dto);

        await BeforePersistence(entity, dto, ct);

        await _unitOfWork.CommitAsync(ct);

        await AfterPersistence(entity, dto, ct);

        return _resultFactory.Success(MapToResponseDto(entity));
    }


    /// <summary>Define o que é carregado junto (inclua só o que o DTO de resposta ou a regra usa).</summary>
    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;

    /// <summary>Aplica o DTO à entidade. Por padrão delega para <see cref="IUpdatable{T}.Update"/>.</summary>
    protected virtual void UpdateEntity(TEntity entity, TRequest request)
    {
        if (entity is IUpdatable<TRequest> updatable)
            updatable.Update(request);
    }

    /// <summary>Mapeia a entidade atualizada para o DTO de resposta.</summary>
    protected abstract TResponse MapToResponseDto(TEntity entity);

    /// <summary>
    /// Regras de negócio que exigem banco ou o estado atual da entidade. Executado após o FluentValidation e antes de
    /// <see cref="UpdateEntity"/>. Retorne <c>null</c> para indicar sucesso.
    /// </summary>
    protected virtual Task<ResultDto<TResponse>?> ValidateBusinessRules(TEntity entity, TRequest dto, CancellationToken ct) => Task.FromResult<ResultDto<TResponse>?>(null);

    /// <summary>Executado após <see cref="UpdateEntity"/> e antes do commit.</summary>
    protected virtual Task BeforePersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Executado após o commit bem-sucedido.</summary>
    protected virtual Task AfterPersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;

    protected static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
