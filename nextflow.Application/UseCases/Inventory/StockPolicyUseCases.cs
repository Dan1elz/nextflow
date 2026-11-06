using FluentValidation;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>T13: as três classes do seed. A contagem de produtos fica em 0 até a análise (F3) classificar (RN-CE29).</summary>
public class GetStockPoliciesUseCase(
    IStockPolicyRepository policyRepository,
    IProductRepository productRepository,
    IResultFactory<List<StockPolicyDto>> resultFactory) : IGetStockPoliciesUseCase
{
    public async Task<ResultDto<List<StockPolicyDto>>> Execute(CancellationToken ct = default)
    {
        var policies = await policyRepository.GetAll(_ => true, 0, int.MaxValue, orderBy: q => q.OrderBy(p => p.AbcClass), ct: ct);
        var counts = await productRepository.CountByAbcClass(ct);

        return resultFactory.Success([.. policies.Select(p => new StockPolicyDto(p, counts.GetValueOrDefault(p.AbcClass)))]);
    }
}

/// <summary>Edita cobertura alvo e dias de segurança de uma classe (RN-CE29). A classe em si não muda.</summary>
public class UpdateStockPolicyUseCase(
    IStockPolicyRepository policyRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork,
    IValidator<StockPolicyRequestDto> validator,
    IResultFactory<StockPolicyDto> resultFactory)
    : UpdateUseCaseBase<StockPolicy, IStockPolicyRepository, StockPolicyRequestDto, StockPolicyDto>(
        policyRepository, unitOfWork, validator, resultFactory)
{
    private int _productCount;

    protected override async Task AfterPersistence(StockPolicy entity, StockPolicyRequestDto dto, CancellationToken ct)
        => _productCount = await productRepository.Count(p => p.IsActive && p.AbcClass == entity.AbcClass, ct);

    protected override StockPolicyDto MapToResponseDto(StockPolicy entity) => new(entity, _productCount);
}
