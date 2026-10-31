using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>
/// Criar loja cria o local "Loja", vendável, como padrão de venda e de recebimento, e vincula os Admins ativos, tudo no
/// mesmo commit (DOMAIN_LEVELS, par 1; decisão 6 de 29/10).
/// </summary>
public class CreateStoreUseCase(
    IStoreRepository storeRepository,
    IStoreMembership storeMembership,
    IUnitOfWork unitOfWork,
    IValidator<StoreRequestDto> validator,
    IResultFactory<StoreResponseDto> resultFactory)
    : CreateUseCaseBase<Store, IStoreRepository, StoreRequestDto, StoreResponseDto>(
        storeRepository, unitOfWork, validator, resultFactory)
{
    protected override Store MapToEntity(StoreRequestDto dto) => new(dto);

    protected override async Task BeforePersistence(Store entity, StoreRequestDto dto, CancellationToken ct)
    {
        var location = new StockLocation(new LocationRequestDto
        {
            StoreId = entity.Id,
            Name = StockLocation.DefaultName,
            IsSellable = true,
        });
        entity.Locations.Add(location);
        entity.SetDefaults(location.Id, location.Id);

        await storeMembership.LinkStore(entity.Id, ct);
    }

    protected override StoreResponseDto MapToResponseDto(Store entity) => new(entity);
}

/// <summary>Edita o nome e os padrões: locais ativos da própria loja, e o de venda precisa ser vendável.</summary>
public class UpdateStoreUseCase(
    IStoreRepository storeRepository,
    IUnitOfWork unitOfWork,
    IValidator<UpdateStoreRequestDto> validator,
    IResultFactory<StoreResponseDto> resultFactory)
    : UpdateUseCaseBase<Store, IStoreRepository, UpdateStoreRequestDto, StoreResponseDto>(
        storeRepository, unitOfWork, validator, resultFactory)
{
    protected override Func<IQueryable<Store>, IQueryable<Store>> GetInclude() => q => q.Include(s => s.Locations);

    protected override Task<ResultDto<StoreResponseDto>?> ValidateBusinessRules(Store entity, UpdateStoreRequestDto dto, CancellationToken ct)
    {
        var sale = entity.Locations.FirstOrDefault(l => l.Id == dto.DefaultSaleLocationId && l.IsActive);
        if (sale is null)
            return Field(nameof(dto.DefaultSaleLocationId), "Escolha um local ativo desta loja.");

        if (!sale.IsSellable)
            return Field(nameof(dto.DefaultSaleLocationId), "O local padrão de venda precisa ser vendável.");

        return entity.Locations.Any(l => l.Id == dto.DefaultReceiveLocationId && l.IsActive)
            ? Task.FromResult<ResultDto<StoreResponseDto>?>(null)
            : Field(nameof(dto.DefaultReceiveLocationId), "Escolha um local ativo desta loja.");
    }

    protected override StoreResponseDto MapToResponseDto(Store entity) => new(entity);

    private Task<ResultDto<StoreResponseDto>?> Field(string field, string message)
        => Task.FromResult<ResultDto<StoreResponseDto>?>(_resultFactory.FieldFailure(field, message));
}

/// <summary>Inativa a loja (RN-CE36): os locais ficam como estão, e a última loja ativa não sai.</summary>
public class DeleteStoreUseCase(
    IStoreRepository storeRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Store, IStoreRepository>(storeRepository, unitOfWork, resultFactory)
{
    protected override async Task<ResultDto<bool>?> ValidateBusinessRules(Store entity, Guid? userId, CancellationToken ct)
    {
        var id = entity.Id;
        return await _repository.Exists(s => s.Id != id && s.IsActive, ct)
            ? null
            : _resultFactory.Failure("Não é possível inativar a última loja ativa.", 400);
    }
}

/// <summary>Reativa a loja e vincula os Admins criados enquanto ela estava inativa (decisão 6 de 29/10).</summary>
public class ReactivateStoreUseCase(
    IStoreRepository storeRepository,
    IStoreMembership storeMembership,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Store, IStoreRepository>(storeRepository, unitOfWork, resultFactory)
{
    protected override Task BeforePersistence(Store entity, Guid? userId, CancellationToken ct)
        => storeMembership.LinkStore(entity.Id, ct);
}

public class GetStoreByIdUseCase(
    IStoreRepository storeRepository,
    IResultFactory<StoreResponseDto> resultFactory)
    : GetByIdUseCaseBase<Store, IStoreRepository, StoreResponseDto>(storeRepository, resultFactory)
{
    protected override Func<IQueryable<Store>, IQueryable<Store>> GetInclude() => q => q.Include(s => s.Locations);

    protected override StoreResponseDto MapToResponseDto(Store entity) => new(entity);
}

/// <summary>Listagem (T7), com os locais. Filtros: <c>search</c> (nome), <c>isActive</c>.</summary>
public class GetAllStoresUseCase(
    IStoreRepository storeRepository,
    IResultFactory<PaginationDto<StoreResponseDto>> resultFactory)
    : GetAllUseCaseBase<Store, IStoreRepository, StoreResponseDto>(storeRepository, resultFactory)
{
    protected override Expression<Func<Store, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        return new FilterExpressionBuilder<Store>()
            .WhereStringContainsAny(set, "search", s => s.Name)
            .WhereBoolEquals(set, "isActive", s => s.IsActive)
            .Predicate;
    }

    protected override Func<IQueryable<Store>, IQueryable<Store>> GetInclude() => q => q.Include(s => s.Locations);

    protected override Func<IQueryable<Store>, IOrderedQueryable<Store>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override StoreResponseDto MapToResponseDto(Store entity) => new(entity);
}

public class GetStoreOptionsUseCase(
    IStoreRepository storeRepository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<Store, IStoreRepository, Option>(storeRepository, resultFactory)
{
    protected override Expression<Func<Store, bool>> BuildSearch(string term) => s => s.Name.ToLower().Contains(term);

    protected override Func<IQueryable<Store>, IOrderedQueryable<Store>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override Option MapToOption(Store entity) => new(entity.Id.ToString(), entity.Name);
}
