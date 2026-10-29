using FluentValidation;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>
/// Base para casos de uso de criação. Orquestra: validação de formato (FluentValidation, 422 por campo)
/// → regras de negócio → mapeamento → <see cref="BeforePersistence"/> → commit único → <see cref="AfterPersistence"/>.
/// </summary>
public abstract class CreateUseCaseBase<TEntity, TRepository, TRequest, TResponse>(
    TRepository repository,
    IUnitOfWork unitOfWork,
    IValidator<TRequest> validator,
    IResultFactory<TResponse> resultFactory)
    : ICreateUseCase<TRequest, TResponse>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IUnitOfWork _unitOfWork = unitOfWork;
    protected readonly IValidator<TRequest> _validator = validator;
    protected readonly IResultFactory<TResponse> _resultFactory = resultFactory;

    public virtual async Task<ResultDto<TResponse>> Execute(TRequest dto, CancellationToken ct = default)
    {
        if (dto is null)
            return _resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validationResult = await _validator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
            return _resultFactory.Failure(validationResult, 422);

        var businessRuleResult = await ValidateBusinessRules(dto, ct);
        if (businessRuleResult != null && !businessRuleResult.IsSuccess)
            return businessRuleResult;

        var entity = MapToEntity(dto);

        await BeforePersistence(entity, dto, ct);

        await _repository.AddAsync(entity, ct);
        await _unitOfWork.CommitAsync(ct);

        await AfterPersistence(entity, dto, ct);

        return _resultFactory.Success(MapToResponseDto(entity));
    }

    /// <summary>Constrói a entidade de domínio a partir do DTO de entrada.</summary>
    protected abstract TEntity MapToEntity(TRequest dto);

    /// <summary>Mapeia a entidade persistida para o DTO de resposta.</summary>
    protected abstract TResponse MapToResponseDto(TEntity entity);

    /// <summary>
    /// Valida regras de negócio que exigem acesso ao banco (ex.: unicidade de e-mail, CPF).
    /// Executado após o FluentValidation e antes de <see cref="MapToEntity"/>. Retorne <c>null</c> para indicar sucesso.
    /// </summary>
    protected virtual Task<ResultDto<TResponse>?> ValidateBusinessRules(TRequest dto, CancellationToken ct) => Task.FromResult<ResultDto<TResponse>?>(null);

    /// <summary>
    /// Executado após <see cref="MapToEntity"/> e antes do commit. Use para registrar entidades dependentes no mesmo
    /// commit (ex.: o local "Loja" ao criar a loja, lançamentos no <c>IStockLedger</c>).
    /// </summary>
    protected virtual Task BeforePersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Executado após o commit bem-sucedido. Use para efeitos externos (notificação, fila, storage).</summary>
    protected virtual Task AfterPersistence(TEntity entity, TRequest dto, CancellationToken ct) => Task.CompletedTask;
}
