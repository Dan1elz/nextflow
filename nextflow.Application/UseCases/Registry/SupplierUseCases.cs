using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Application.UseCases.Registry;

public class CreateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IValidator<SupplierRequestDto> validator,
    IResultFactory<SupplierResponseDto> resultFactory)
    : CreateUseCaseBase<Supplier, ISupplierRepository, SupplierRequestDto, SupplierResponseDto>(
        supplierRepository, unitOfWork, validator, resultFactory)
{
    /// <summary>CNPJ único (RN-CD02): erro no campo.</summary>
    protected override async Task<ResultDto<SupplierResponseDto>?> ValidateBusinessRules(SupplierRequestDto dto, CancellationToken ct)
    {
        var cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        return await _repository.Exists(s => s.Cnpj == cnpj, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cnpj), "Já existe um fornecedor com este CNPJ.")
            : null;
    }

    protected override Supplier MapToEntity(SupplierRequestDto dto) => new(dto);

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

public class UpdateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IValidator<SupplierRequestDto> validator,
    IResultFactory<SupplierResponseDto> resultFactory)
    : UpdateUseCaseBase<Supplier, ISupplierRepository, SupplierRequestDto, SupplierResponseDto>(
        supplierRepository, unitOfWork, validator, resultFactory)
{
    protected override async Task<ResultDto<SupplierResponseDto>?> ValidateBusinessRules(Supplier entity, SupplierRequestDto dto, CancellationToken ct)
    {
        var cnpj = TaxDocument.ParseCnpj(dto.Cnpj);
        var id = entity.Id;
        return await _repository.Exists(s => s.Cnpj == cnpj && s.Id != id, ct)
            ? _resultFactory.FieldFailure(nameof(dto.Cnpj), "Já existe um fornecedor com este CNPJ.")
            : null;
    }

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

public class DeleteSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Supplier, ISupplierRepository>(supplierRepository, unitOfWork, resultFactory);

public class ReactivateSupplierUseCase(
    ISupplierRepository supplierRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : ReactivateUseCaseBase<Supplier, ISupplierRepository>(supplierRepository, unitOfWork, resultFactory);

public class GetSupplierByIdUseCase(
    ISupplierRepository supplierRepository,
    IOwnerDirectory ownerDirectory,
    IResultFactory<SupplierResponseDto> resultFactory)
    : GetByIdUseCaseBase<Supplier, ISupplierRepository, SupplierResponseDto>(supplierRepository, resultFactory)
{
    public override async Task<ResultDto<SupplierResponseDto>> Execute(Guid id, CancellationToken ct = default)
    {
        var result = await base.Execute(id, ct);
        if (result.IsSuccess)
        {
            result.Data!.MainAddress = await ownerDirectory.GetMainAddress(OwnerTypeEnum.Supplier, id, ct);
            result.Data.MainContact = await ownerDirectory.GetMainContact(OwnerTypeEnum.Supplier, id, ct);
        }
        return result;
    }

    protected override SupplierResponseDto MapToResponseDto(Supplier entity) => new(entity);
}

/// <summary>
/// Listagem (T-CD1). Filtros: <c>search</c> (nome, CNPJ), <c>isActive</c>, <c>cityId</c>, <c>stateId</c>, <c>purchases</c>
/// (ver <see cref="RegistryListFilters.Purchases"/>) e <c>pendingOrders</c>. Última compra e prazo real vêm dos pedidos.
/// </summary>
public class GetAllSuppliersUseCase(
    ISupplierRepository supplierRepository,
    IAddressRepository addressRepository,
    IPurchaseOrderRepository purchaseOrderRepository,
    IOwnerDirectory ownerDirectory,
    IBusinessClock clock,
    IResultFactory<PaginationDto<SupplierListItemDto>> resultFactory)
    : GetAllUseCaseBase<Supplier, ISupplierRepository, SupplierListItemDto>(supplierRepository, resultFactory)
{
    private readonly List<IdFilter> _idFilters = [];
    private IReadOnlyDictionary<Guid, OwnerSummaryDto> _summaries = new Dictionary<Guid, OwnerSummaryDto>();
    private Dictionary<Guid, PurchaseStatsDto> _stats = [];

    public override async Task<ResultDto<PaginationDto<SupplierListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
    {
        var set = new FilterSet(filters);
        _idFilters.Clear();

        if (await RegistryListFilters.Place(set, addressRepository, OwnerTypeEnum.Supplier, ct) is { } place)
            _idFilters.Add(place);
        if (await RegistryListFilters.Purchases(set, clock, purchaseOrderRepository.GetSupplierIdsWithPurchase, ct) is { } purchases)
            _idFilters.Add(purchases);
        if (set.TryGetString("pendingOrders", out var rawPending) && FilterValueParsers.TryParseBool(rawPending, out var pending))
            _idFilters.Add(new IdFilter(await purchaseOrderRepository.GetSupplierIdsWithPendingOrders(ct), pending));

        return await base.Execute(offset, limit, filters, sort, order, ct);
    }

    protected override Expression<Func<Supplier, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Supplier>().WhereBoolEquals(set, "isActive", s => s.IsActive);

        if (set.TryGetString("search", out var search))
            builder.And(SupplierSearch.By(search));

        foreach (var filter in _idFilters)
        {
            var ids = filter.Ids;
            builder.And(filter.Include ? s => ids.Contains(s.Id) : s => !ids.Contains(s.Id));
        }

        return builder.Predicate;
    }

    protected override async Task PrepareMapping(IReadOnlyList<Supplier> items, CancellationToken ct)
    {
        var ids = items.Select(s => s.Id).ToList();
        _summaries = await ownerDirectory.GetSummaries(OwnerTypeEnum.Supplier, ids, ct);
        _stats = await purchaseOrderRepository.GetSupplierStats(ids, ct);
    }

    protected override Func<IQueryable<Supplier>, IQueryable<Supplier>> GetInclude()
        => q => q.Include(s => s.Products.Where(link => link.Product.IsActive));

    protected override Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override IReadOnlyDictionary<string, Expression<Func<Supplier, object?>>> GetSortableFields()
        => new Dictionary<string, Expression<Func<Supplier, object?>>>
        {
            ["name"] = s => s.Name,
            ["leadTimeDays"] = s => s.LeadTimeDays,
            ["createdAt"] = s => s.CreatedAt,
        };

    protected override SupplierListItemDto MapToResponseDto(Supplier entity)
    {
        var dto = new SupplierListItemDto(entity, _summaries.GetValueOrDefault(entity.Id));
        if (_stats.TryGetValue(entity.Id, out var stats))
        {
            dto.LastPurchaseOn = stats.LastAt is { } last ? clock.ToBusinessDate(last) : null;
            dto.RealLeadTimeDays = stats.AverageRealLeadTimeDays is { } average ? (int)Math.Round(average) : null;
        }
        return dto;
    }
}

/// <summary>Combo de fornecedor: só ativos, label "nome · CNPJ" (T-CD2).</summary>
public class GetSupplierOptionsUseCase(
    ISupplierRepository supplierRepository,
    IResultFactory<List<Option>> resultFactory)
    : GetOptionsUseCaseBase<Supplier, ISupplierRepository, Option>(supplierRepository, resultFactory)
{
    protected override Expression<Func<Supplier, bool>> BuildSearch(string term) => SupplierSearch.By(term);

    protected override Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>> GetOrderBy()
        => q => q.OrderBy(s => s.Name).ThenBy(s => s.Id);

    protected override Option MapToOption(Supplier entity) => new(entity.Id.ToString(), $"{entity.Name} · {entity.Cnpj.Masked}");
}

internal static class SupplierSearch
{
    /// <summary>
    /// Nome ou dígitos do CNPJ. O cast por <c>object</c> faz o EF comparar a coluna de texto em vez do value object:
    /// só vale traduzido para SQL.
    /// </summary>
    public static Expression<Func<Supplier, bool>> By(string search)
    {
        var term = search.Trim().ToLowerInvariant();
        var digits = Digits.Only(term);
        return digits.Length > 0
            ? s => s.Name.ToLower().Contains(term) || ((string)(object)s.Cnpj).Contains(digits)
            : s => s.Name.ToLower().Contains(term);
    }
}

/// <summary>
/// Aba Produtos do fornecedor (T-CD2): produtos vinculados, ativos primeiro, o preferencial antes, depois por nome.
/// </summary>
public class GetSupplierProductsUseCase(
    ISupplierRepository supplierRepository,
    IResultFactory<List<SupplierProductDto>> resultFactory)
    : GetByIdUseCaseBase<Supplier, ISupplierRepository, List<SupplierProductDto>>(supplierRepository, resultFactory)
{
    protected override List<SupplierProductDto> MapToResponseDto(Supplier entity)
        => [.. entity.Products.Select(link => new SupplierProductDto(link, entity))
            .OrderByDescending(p => p.ProductIsActive).ThenByDescending(p => p.IsPreferred).ThenBy(p => p.ProductName)];

    protected override Func<IQueryable<Supplier>, IQueryable<Supplier>> GetInclude()
        => q => q.Include(s => s.Products).ThenInclude(link => link.Product);
}
