using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Inventory;

/// <summary>
/// Lançamento manual (T9): valida o formato, entrega uma linha ao <see cref="IStockLedger"/> como documento
/// <see cref="SourceTypeEnum.Manual"/> e commita. As falhas do livro-razão (local fechado, negativo, RN-CE32) voltam no campo
/// do formulário. Não há exclusão: correção é um lançamento contrário (RN-CE14).
/// </summary>
public class CreateManualMovementUseCase(
    IStockLedger ledger,
    ILotRepository lotRepository,
    IUnitOfWork unitOfWork,
    IValidator<ManualMovementRequestDto> validator,
    IResultFactory<bool> resultFactory) : ICreateManualMovementUseCase
{
    private const string LinePrefix = "Lines[0].";

    public async Task<ResultDto<bool>> Execute(ManualMovementRequestDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        // Lote novo (F1.7): código + validade; o mesmo código com a mesma validade reaproveita o lote da variação.
        var lotId = dto.LotId;
        if (dto.NewLot is { } newLot)
        {
            var known = await lotRepository.GetForVariants([dto.VariantId], ct);
            var (lot, error) = await LotRegistry.FindOrCreate(lotRepository, known, dto.VariantId, newLot.Code, newLot.ExpiresOn, ct);
            if (error is not null)
                return resultFactory.FieldFailure("newLot.code", error);
            lotId = lot!.Id;
        }

        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Manual, UserId = userId, Note = dto.Note };
        var line = new StockLineDto
        {
            VariantId = dto.VariantId,
            LocationId = dto.LocationId,
            LotId = lotId,
            Type = dto.Type,
            Reason = dto.Reason,
            Quantity = dto.Quantity,
            UnitCost = dto.UnitCost,
        };

        var failures = await ledger.Post(document, [line], ct);
        if (failures.Count > 0)
            return resultFactory.Failure(new ValidationResult(failures.Select(ToFormField)), 422);

        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(true);
    }

    /// <summary>"Lines[0].Quantity" do livro-razão vira "quantity", o campo do drawer.</summary>
    private static ValidationFailure ToFormField(ValidationFailure failure)
        => new(failure.PropertyName.StartsWith(LinePrefix) ? failure.PropertyName[LinePrefix.Length..] : failure.PropertyName, failure.ErrorMessage);
}

/// <summary>
/// Kardex (T8). O repositório do livro-razão não herda a base de CRUD (não expõe update nem delete, RN-CE14), por isso a
/// listagem não usa <c>GetAllUseCaseBase</c>; segue o mesmo contrato de paginação e filtros. O saldo após vem gravado em
/// cada movimentação pelo <see cref="IStockLedger"/> (RN-CE01): nada é recalculado aqui.
/// </summary>
public class GetStockMovementsUseCase(
    IStockMovementRepository movementRepository,
    IBusinessClock clock,
    IResultFactory<PaginationDto<StockMovementListItemDto>> resultFactory) : IGetStockMovementsUseCase
{
    public async Task<ResultDto<PaginationDto<StockMovementListItemDto>>> Execute(
        int offset, int limit, IReadOnlyDictionary<string, string>? filters, CancellationToken ct = default)
    {
        (offset, limit) = PagingLimits.Normalize(offset, limit);
        var predicate = BuildFilterExpression(new FilterSet(filters));

        var items = await movementRepository.GetAll(
            predicate, offset, limit, GetInclude(), q => q.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id), ct);
        var total = await movementRepository.Count(predicate, ct);

        return resultFactory.Success(PaginationDto<StockMovementListItemDto>.Paginate(items.Select(m => new StockMovementListItemDto(m)), total));
    }

    private Expression<Func<StockMovement, bool>> BuildFilterExpression(FilterSet set)
    {
        var builder = new FilterExpressionBuilder<StockMovement>()
            .WhereGuidEquals(set, "locationId", m => m.LocationId)
            .WhereGuidEquals(set, "variantId", m => m.VariantId)
            .WhereEnumEquals(set, "reason", m => m.Reason)
            .WhereEnumEquals(set, "sourceType", m => m.SourceType);

        if (set.TryGetString("storeId", out var rawStore) && Guid.TryParse(rawStore, out var storeId))
            builder.And(m => m.Location.StoreId == storeId);
        if (set.TryGetString("productId", out var rawProduct) && Guid.TryParse(rawProduct, out var productId))
            builder.And(m => m.Variant.ProductId == productId);

        // Período em dias de negócio (America/Sao_Paulo): "to" inclui o dia inteiro.
        if (set.TryGetString("from", out var rawFrom) && FilterValueParsers.TryParseDateOnly(rawFrom, out var from))
        {
            var start = clock.StartOfDayUtc(from);
            builder.And(m => m.CreatedAt >= start);
        }
        if (set.TryGetString("to", out var rawTo) && FilterValueParsers.TryParseDateOnly(rawTo, out var to))
        {
            var end = clock.StartOfDayUtc(to.AddDays(1));
            builder.And(m => m.CreatedAt < end);
        }

        return builder.Predicate;
    }

    private static Func<IQueryable<StockMovement>, IQueryable<StockMovement>> GetInclude()
        => q => q.Include(m => m.Variant).ThenInclude(v => v.Product)
            .Include(m => m.Variant).ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute)
            .Include(m => m.Location)
            .Include(m => m.Lot)
            .Include(m => m.User);
}
