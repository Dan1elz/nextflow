using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases.Base;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Base;

/// <summary>Base para casos de uso de busca por id com mapeamento para o DTO de resposta.</summary>
public abstract class GetByIdUseCaseBase<TEntity, TRepository, TResponse>(
    TRepository repository,
    IResultFactory<TResponse> resultFactory)
    : IGetByIdUseCase<TEntity, TResponse>
    where TEntity : BaseModel
    where TRepository : IBaseRepository<TEntity>
{
    protected readonly TRepository _repository = repository;
    protected readonly IResultFactory<TResponse> _resultFactory = resultFactory;

    public virtual async Task<ResultDto<TResponse>> Execute(Guid id, CancellationToken ct = default)
    {
        var entity = await _repository.GetById(id, GetInclude(), ct);

        if (entity == null)
            return _resultFactory.Failure($"Entidade com id {id} não encontrada.", 404);

        var businessRuleError = ValidateBusinessRules(entity);
        if (businessRuleError != null && !businessRuleError.IsSuccess)
            return businessRuleError;

        return _resultFactory.Success(MapToResponseDto(entity));
    }

    /// <summary>Regras sobre a entidade encontrada antes de devolvê-la. Retorne <c>null</c> para indicar sucesso.</summary>
    protected virtual ResultDto<TResponse>? ValidateBusinessRules(TEntity entity) => null;

    /// <summary>Mapeia a entidade encontrada para o DTO de resposta.</summary>
    protected abstract TResponse MapToResponseDto(TEntity entity);

    /// <summary>Define o que é carregado junto (inclua só o que o DTO de resposta mapeia).</summary>
    protected virtual Func<IQueryable<TEntity>, IQueryable<TEntity>>? GetInclude() => null;
}
