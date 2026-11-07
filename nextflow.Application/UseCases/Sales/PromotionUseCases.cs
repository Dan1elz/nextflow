using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Filters;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Promotions;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Promotions;

namespace Nextflow.Application.UseCases.Sales;

/// <summary>Escopo da promoção: a variação escolhida ou as variações não removidas do produto (T-VD7).</summary>
internal static class PromotionScope
{
    public static List<ProductVariant> Variants(Product product, Guid? variantId)
        => [.. product.Variants
            .Where(v => v.RemovedOn == null && (variantId == null || v.Id == variantId))
            .OrderBy(v => v.SortOrder)];

    public static bool IsBelowCost(ProductVariant variant, decimal promoPrice)
        => variant.AverageCost is { } cost && promoPrice < cost;
}

/// <summary>
/// Cria promoção manual (T-VD7). Formato no validador (RN-VD10); aqui: início a partir de hoje (data de negócio), produto
/// ativo, variação do produto e não removida, sem sobreposição no mesmo escopo (RN-VD11 com L-VD10) e o aviso de preço
/// abaixo do custo médio, que pede confirmação e fica gravado (RN-VD13).
/// </summary>
public class CreatePromotionUseCase(
    IPromotionRepository promotionRepository,
    IProductRepository productRepository,
    IProductPricingService pricing,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IValidator<PromotionRequestDto> validator,
    IResultFactory<PromotionListItemDto> resultFactory)
    : CreateUseCaseBase<Promotion, IPromotionRepository, PromotionRequestDto, PromotionListItemDto>(
        promotionRepository, unitOfWork, validator, resultFactory)
{
    private bool _belowCost;
    private Promotion? _saved;

    protected override async Task<ResultDto<PromotionListItemDto>?> ValidateBusinessRules(PromotionRequestDto dto, CancellationToken ct)
    {
        if (UserId is null)
            return _resultFactory.Failure("Usuário não identificado.", 401);

        if (dto.StartDate < clock.Today)
            return _resultFactory.FieldFailure("startDate", "O início deve ser hoje ou depois.");

        var product = await productRepository.GetById(dto.ProductId, GetProductInclude(), ct);
        if (product is null || !product.IsActive)
            return _resultFactory.FieldFailure("productId", "Produto não encontrado ou inativo.");

        var scope = PromotionScope.Variants(product, dto.VariantId);
        if (dto.VariantId is not null && scope.Count == 0)
            return _resultFactory.FieldFailure("variantId", "A variação não pertence ao produto ou foi removida.");

        var (productId, variantId, start, end) = (dto.ProductId, dto.VariantId, dto.StartDate, dto.EndDate);
        var overlap = await _repository.GetAll(
            p => p.ProductId == productId && p.IsActive && p.StartDate <= end && start <= p.EndDate
                && (p.VariantId == null || variantId == null || p.VariantId == variantId),
            0, 1, ct: ct);
        if (overlap.FirstOrDefault() is { } existing)
            return _resultFactory.FieldFailure("startDate",
                $"Já existe promoção ativa deste produto de {existing.StartDate:dd/MM/yyyy} a {existing.EndDate:dd/MM/yyyy} no mesmo escopo.");

        _belowCost = scope.Any(v => PromotionScope.IsBelowCost(v, pricing.GetPromotionalPrice(v.Price, dto.DiscountPercent)));
        return _belowCost && !dto.ConfirmWarnings
            ? _resultFactory.Confirm("A promoção deixa o preço abaixo do custo médio. Deseja continuar?")
            : null;
    }

    protected override Promotion MapToEntity(PromotionRequestDto dto) => new(dto, UserId.GetValueOrDefault(), _belowCost);

    protected override async Task AfterPersistence(Promotion entity, PromotionRequestDto dto, CancellationToken ct)
        => _saved = await _repository.GetById(entity.Id, GetInclude(), ct);

    protected override PromotionListItemDto MapToResponseDto(Promotion entity) => new(_saved ?? entity, pricing, clock.Today);

    private static Func<IQueryable<Product>, IQueryable<Product>> GetProductInclude()
        => q => q.Include(p => p.Variants);

    private static Func<IQueryable<Promotion>, IQueryable<Promotion>> GetInclude()
        => q => q.Include(p => p.Product).ThenInclude(product => product.Variants)
            .Include(p => p.Variant!).ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute)
            .Include(p => p.CreatedBy);
}

/// <summary>
/// Listagem do T-VD7, início mais recente primeiro. Filtros: <c>status</c> (calculado pela data de negócio), <c>source</c>,
/// <c>productId</c> e o período <c>from</c>/<c>to</c>, que pega as promoções que cruzam o intervalo.
/// </summary>
public class GetAllPromotionsUseCase(
    IPromotionRepository promotionRepository,
    IProductPricingService pricing,
    IBusinessClock clock,
    IResultFactory<PaginationDto<PromotionListItemDto>> resultFactory)
    : GetAllUseCaseBase<Promotion, IPromotionRepository, PromotionListItemDto>(promotionRepository, resultFactory)
{
    protected override Expression<Func<Promotion, bool>> BuildFilterExpression(IReadOnlyDictionary<string, string>? filters)
    {
        var set = new FilterSet(filters);
        var builder = new FilterExpressionBuilder<Promotion>()
            .WhereEnumEquals(set, "source", p => p.Source)
            .WhereGuidEquals(set, "productId", p => p.ProductId);

        if (set.TryGetString("from", out var rawFrom) && FilterValueParsers.TryParseDateOnly(rawFrom, out var from))
            builder.And(p => p.EndDate >= from);
        if (set.TryGetString("to", out var rawTo) && FilterValueParsers.TryParseDateOnly(rawTo, out var to))
            builder.And(p => p.StartDate <= to);

        if (set.TryGetString("status", out var rawStatus) && FilterValueParsers.TryParseEnum<PromotionStatusEnum>(rawStatus, out var status))
        {
            var today = clock.Today;
            builder.And(status switch
            {
                PromotionStatusEnum.Canceled => p => !p.IsActive,
                PromotionStatusEnum.Scheduled => p => p.IsActive && today < p.StartDate,
                PromotionStatusEnum.Active => p => p.IsActive && p.StartDate <= today && today <= p.EndDate,
                _ => p => p.IsActive && p.EndDate < today,
            });
        }

        return builder.Predicate;
    }

    protected override Func<IQueryable<Promotion>, IQueryable<Promotion>> GetInclude()
        => q => q.Include(p => p.Product).ThenInclude(product => product.Variants)
            .Include(p => p.Variant!).ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute)
            .Include(p => p.CreatedBy);

    protected override Func<IQueryable<Promotion>, IOrderedQueryable<Promotion>> GetOrderBy()
        => q => q.OrderByDescending(p => p.StartDate).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id);

    protected override PromotionListItemDto MapToResponseDto(Promotion entity) => new(entity, pricing, clock.Today);
}

/// <summary>Prévia do T-VD7: por variação do escopo, tabela, promocional, custo médio, margem e o aviso de custo (RN-VD13).</summary>
public class GetPromotionPreviewUseCase(
    IProductRepository productRepository,
    IProductPricingService pricing,
    IResultFactory<PromotionPreviewDto> resultFactory) : IGetPromotionPreviewUseCase
{
    public async Task<ResultDto<PromotionPreviewDto>> Execute(Guid productId, Guid? variantId, decimal discountPercent, CancellationToken ct = default)
    {
        if (discountPercent is < PromotionRequestDtoValidator.MinPercent or > PromotionRequestDtoValidator.MaxPercent)
            return resultFactory.FieldFailure("discountPercent", "O desconto deve ser de 1% a 90%.");

        var product = await productRepository.GetById(productId, GetProductInclude(), ct);
        if (product is null || !product.IsActive)
            return resultFactory.FieldFailure("productId", "Produto não encontrado ou inativo.");

        var scope = PromotionScope.Variants(product, variantId);
        if (variantId is not null && scope.Count == 0)
            return resultFactory.FieldFailure("variantId", "A variação não pertence ao produto ou foi removida.");

        return resultFactory.Success(new PromotionPreviewDto
        {
            Variants = [.. scope.Select(v =>
            {
                var promoPrice = pricing.GetPromotionalPrice(v.Price, discountPercent);
                var label = ProductVariantResponseDto.LabelOf(v);
                return new PromotionPreviewVariantDto
                {
                    VariantId = v.Id,
                    VariantLabel = string.IsNullOrEmpty(label) ? null : label,
                    ListPrice = v.Price,
                    PromoPrice = promoPrice,
                    AverageCost = v.AverageCost,
                    MarginPercent = v.AverageCost is { } cost && promoPrice > 0
                        ? Math.Round((promoPrice - cost) / promoPrice * 100, 2, MidpointRounding.AwayFromZero)
                        : null,
                    BelowCost = PromotionScope.IsBelowCost(v, promoPrice),
                };
            })],
        });
    }

    private static Func<IQueryable<Product>, IQueryable<Product>> GetProductInclude()
        => q => q.Include(p => p.Variants)
            .ThenInclude(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);
}

/// <summary>Cancela (inativa) a promoção agendada ou vigente; encerrada ou já cancelada não muda (RN-VD12).</summary>
public class CancelPromotionUseCase(
    IPromotionRepository promotionRepository,
    IBusinessClock clock,
    IUnitOfWork unitOfWork,
    IResultFactory<bool> resultFactory) : ICancelPromotionUseCase
{
    public async Task<ResultDto<bool>> Execute(Guid id, CancellationToken ct = default)
    {
        var promotion = await promotionRepository.GetByIdForUpdate(id, ct: ct);
        if (promotion is null)
            return resultFactory.Failure("Promoção não encontrada.", 404);

        var status = promotion.StatusOn(clock.Today);
        if (status is PromotionStatusEnum.Ended or PromotionStatusEnum.Canceled)
            return resultFactory.Failure($"Só promoção agendada ou vigente pode ser cancelada; esta está {status.GetName().ToLowerInvariant()}.", 400);

        promotion.Cancel();
        await unitOfWork.CommitAsync(ct);
        return resultFactory.Success(true);
    }
}
