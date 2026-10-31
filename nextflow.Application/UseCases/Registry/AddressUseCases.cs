using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Cities;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Registry;

public class CreateAddressUseCase(
    IAddressRepository addressRepository,
    ICityRepository cityRepository,
    IOwnerDirectory ownerDirectory,
    IUnitOfWork unitOfWork,
    IValidator<AddressRequestDto> validator,
    IResultFactory<AddressResponseDto> resultFactory)
    : CreateUseCaseBase<Address, IAddressRepository, AddressRequestDto, AddressResponseDto>(
        addressRepository, unitOfWork, validator, resultFactory)
{
    private CitySummaryDto? _city;

    protected override async Task<ResultDto<AddressResponseDto>?> ValidateBusinessRules(AddressRequestDto dto, CancellationToken ct)
    {
        var ownerError = await OwnerRules.CheckOwner(ownerDirectory, _resultFactory, dto.OwnerType, dto.OwnerId, ct);
        if (ownerError != null)
            return ownerError;

        _city = await cityRepository.GetSummary(dto.CityId, ct);
        return _city is null ? _resultFactory.FieldFailure(nameof(dto.CityId), "Cidade não encontrada.") : null;
    }

    protected override Address MapToEntity(AddressRequestDto dto) => new(dto);

    protected override Task BeforePersistence(Address entity, AddressRequestDto dto, CancellationToken ct)
        => OwnerRules.ApplyMainFlagOnCreate(_repository, entity, ct);

    protected override AddressResponseDto MapToResponseDto(Address entity) => new(entity, _city);
}

public class UpdateAddressUseCase(
    IAddressRepository addressRepository,
    ICityRepository cityRepository,
    IOwnerDirectory ownerDirectory,
    IUnitOfWork unitOfWork,
    IValidator<UpdateAddressRequestDto> validator,
    IResultFactory<AddressResponseDto> resultFactory)
    : UpdateUseCaseBase<Address, IAddressRepository, UpdateAddressRequestDto, AddressResponseDto>(
        addressRepository, unitOfWork, validator, resultFactory)
{
    private CitySummaryDto? _city;

    protected override async Task<ResultDto<AddressResponseDto>?> ValidateBusinessRules(Address entity, UpdateAddressRequestDto dto, CancellationToken ct)
    {
        var ownerError = await OwnerRules.CheckOwner(ownerDirectory, _resultFactory, entity.OwnerType, entity.OwnerId, ct);
        if (ownerError != null)
            return ownerError;

        _city = await cityRepository.GetSummary(dto.CityId, ct);
        return _city is null ? _resultFactory.FieldFailure(nameof(dto.CityId), "Cidade não encontrada.") : null;
    }

    protected override Task BeforePersistence(Address entity, UpdateAddressRequestDto dto, CancellationToken ct)
        => OwnerRules.ApplyMainFlagOnUpdate(_repository, entity, ct);

    protected override AddressResponseDto MapToResponseDto(Address entity) => new(entity, _city);
}

public class DeleteAddressUseCase(
    IAddressRepository addressRepository,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory)
    : DeleteUseCaseBase<Address, IAddressRepository>(addressRepository, unitOfWork, resultFactory);

/// <summary>Endereços de um dono: <c>ownerType</c> e <c>ownerId</c> são obrigatórios (decisão 4 de 29/10).</summary>
public class GetAllAddressesUseCase(
    IAddressRepository addressRepository,
    IResultFactory<PaginationDto<AddressResponseDto>> resultFactory)
    : GetAllUseCaseBase<Address, IAddressRepository, AddressResponseDto>(addressRepository, resultFactory)
{
    public override Task<ResultDto<PaginationDto<AddressResponseDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters,
        string? sort = null, string? order = null, CancellationToken ct = default)
        => OwnerRules.TryGetOwner(filters, out _, out _)
            ? base.Execute(offset, limit, filters, sort, order, ct)
            : Task.FromResult(_resultFactory.Failure("Informe ownerType e ownerId nos filtros.", 400));

    protected override Expression<Func<Address, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        OwnerRules.TryGetOwner(filters, out var ownerType, out var ownerId);
        return a => a.OwnerType == ownerType && a.OwnerId == ownerId;
    }

    protected override Func<IQueryable<Address>, IQueryable<Address>> GetInclude()
        => q => q.Include(a => a.City).ThenInclude(c => c.State);

    protected override Func<IQueryable<Address>, IOrderedQueryable<Address>> GetOrderBy()
        => q => q.OrderByDescending(a => a.IsMain).ThenBy(a => a.CreatedAt);

    protected override AddressResponseDto MapToResponseDto(Address entity) => new(entity);
}
