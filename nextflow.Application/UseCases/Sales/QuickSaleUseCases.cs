using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Orders;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;

/// <summary>Repositórios e serviços que montam o carrinho da venda rápida; a venda e a prévia usam o mesmo cálculo.</summary>
internal sealed record QuickSaleSources(
    IStoreRepository Stores,
    IStockLocationRepository Locations,
    IClientRepository Clients,
    IProductVariantRepository Variants,
    IProductPricingService Pricing,
    IBusinessClock Clock);

/// <summary>Carrinho montado: o pedido (ainda fora do banco), a promoção de cada linha e as falhas por campo.</summary>
internal sealed class QuickSaleCart
{
    public List<ValidationFailure> Failures { get; } = [];
    public Order? Order { get; private set; }
    public List<Promotion?> Promotions { get; } = [];
    /// <summary>Algum item chegou com <c>expectedUnitPrice</c> diferente do preço efetivo atual.</summary>
    public bool PriceChanged { get; private set; }

    /// <summary>
    /// Resolve loja, local de venda (o informado, ativo, vendável e da loja, ou o padrão), cliente e variações, e monta os
    /// itens com o preço efetivo do dia pelo <see cref="IProductPricingService"/> (RN-VD02). Variação inativa e produto fora
    /// de linha vendem (RN-CE19); variação removida ou produto inativo, não.
    /// </summary>
    public static async Task<QuickSaleCart> Build(QuickSaleDto dto, QuickSaleSources sources, Guid userId, CancellationToken ct)
    {
        var cart = new QuickSaleCart();

        var store = await sources.Stores.GetById(dto.StoreId, ct: ct);
        if (store is null || !store.IsActive)
        {
            cart.Failures.Add(new("storeId", "Loja não encontrada ou inativa."));
            return cart;
        }

        if ((dto.SaleLocationId ?? store.DefaultSaleLocationId) is not { } locationId)
        {
            cart.Failures.Add(new("saleLocationId", "A loja não tem local de venda padrão."));
            return cart;
        }

        var location = await sources.Locations.GetById(locationId, ct: ct);
        if (location is null || !location.IsActive || !location.IsSellable || location.StoreId != store.Id)
        {
            cart.Failures.Add(new("saleLocationId", "O local precisa ser um local de venda ativo da loja."));
            return cart;
        }

        if (dto.ClientId is { } clientId && await sources.Clients.GetById(clientId, ct: ct) is not { IsActive: true })
            cart.Failures.Add(new("clientId", "Cliente não encontrado ou inativo."));

        var variantIds = dto.Items.Select(i => i.VariantId).Distinct().ToList();
        var variants = (await sources.Variants.GetAll(
                v => variantIds.Contains(v.Id), 0, int.MaxValue, GetVariantInclude(), ct: ct))
            .ToDictionary(v => v.Id);

        var order = new Order(dto, locationId, userId, sources.Clock.UtcNow);
        for (var i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            if (!variants.TryGetValue(item.VariantId, out var variant) || variant.RemovedOn != null || !variant.Product.IsActive)
            {
                cart.Failures.Add(new($"items[{i}].variantId", "Produto não encontrado, removido ou inativo."));
                continue;
            }

            if (decimal.Round(item.Quantity, variant.Product.DecimalPlaces) != item.Quantity)
                cart.Failures.Add(new($"items[{i}].quantity",
                    $"A quantidade aceita no máximo {variant.Product.DecimalPlaces} casa(s) decimal(is)."));

            var unitPrice = sources.Pricing.GetEffectivePrice(variant);
            if (item.Discount > OrderItem.GrossOf(unitPrice, item.Quantity))
                cart.Failures.Add(new($"items[{i}].discount", "O desconto não pode passar do preço × quantidade."));
            if (item.ExpectedUnitPrice is { } expected && expected != unitPrice)
                cart.PriceChanged = true;

            var promotion = sources.Pricing.GetActivePromotion(variant);
            cart.Promotions.Add(promotion);
            order.AddItem(new OrderItem(item, variant, unitPrice, promotion));
        }

        cart.Order = order;
        return cart;
    }

    private static Func<IQueryable<ProductVariant>, IQueryable<ProductVariant>> GetVariantInclude()
        => q => q.Include(v => v.Product).ThenInclude(p => p.Promotions.Where(promotion => promotion.IsActive));
}

/// <summary>
/// Venda rápida de balcão (T-VD2, RN-VD15): grava o pedido já Pago no canal Counter e baixa o estoque do local de venda
/// pelo <see cref="IStockLedger"/> (RN-CE14), tudo num commit. Preço diferente do que o seletor mostrou pede confirmação;
/// a soma dos pagamentos precisa fechar o total (RN-VD05); sem negativo, salvo produto que permite (RN-CE32, no ledger).
/// </summary>
public class CreateQuickSaleUseCase(
    IOrderRepository orderRepository,
    IStoreRepository storeRepository,
    IStockLocationRepository locationRepository,
    IClientRepository clientRepository,
    IProductVariantRepository variantRepository,
    IProductPricingService pricing,
    IBusinessClock clock,
    IStockLedger ledger,
    IUnitOfWork unitOfWork,
    IValidator<QuickSaleDto> validator,
    IResultFactory<OrderDetailDto> resultFactory) : ICreateQuickSaleUseCase
{
    public async Task<ResultDto<OrderDetailDto>> Execute(QuickSaleDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return resultFactory.Failure(validation, 422);

        var cart = await QuickSaleCart.Build(
            dto, new(storeRepository, locationRepository, clientRepository, variantRepository, pricing, clock), userId, ct);
        if (cart.Failures.Count > 0 || cart.Order is not { } order)
            return resultFactory.Failure(new ValidationResult(cart.Failures), 422);

        if (cart.PriceChanged && !dto.ConfirmWarnings)
            return resultFactory.Confirm($"O preço mudou desde a busca. Novo total: {Money(order.Total)}. Deseja continuar?");

        var paid = dto.Payments.Sum(p => p.Amount);
        if (paid != order.Total)
            return resultFactory.FieldFailure("payments",
                $"A soma dos pagamentos ({Money(paid)}) precisa ser igual ao total ({Money(order.Total)}).");

        var cashPaid = dto.Payments.Where(p => p.Method == PaymentMethodEnum.Cash).Sum(p => p.Amount);
        if (dto.CashReceived is { } received)
        {
            if (cashPaid == 0)
                return resultFactory.FieldFailure("cashReceived", "Valor recebido só vale com pagamento em dinheiro.");
            if (received < cashPaid)
                return resultFactory.FieldFailure("cashReceived", $"O valor recebido não cobre o dinheiro ({Money(cashPaid)}).");
            order.RecordCash(received, cashPaid);
        }

        foreach (var payment in dto.Payments)
            order.AddPayment(new Payment(payment));

        var document = new StockDocumentDto { SourceType = SourceTypeEnum.Order, SourceId = order.Id, UserId = userId };
        var lines = order.Items.OrderBy(i => i.Position).Select(i => new StockLineDto
        {
            VariantId = i.VariantId,
            LocationId = order.SaleLocationId,
            Type = MovementTypeEnum.Out,
            Reason = MovementReasonEnum.Sale,
            Quantity = i.Quantity,
        }).ToList();

        var failures = await ledger.Post(document, lines, ct);
        if (failures.Count > 0)
            return resultFactory.Failure(new ValidationResult(failures.Select(ToItemField)), 422);

        await orderRepository.AddAsync(order, ct);
        await unitOfWork.CommitAsync(ct);

        var saved = await orderRepository.GetById(order.Id, GetInclude(), ct);
        return resultFactory.Success(new OrderDetailDto(saved ?? order));
    }

    private static readonly NumberFormatInfo PtBr = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private static string Money(decimal value) => "R$ " + value.ToString("N2", PtBr);

    /// <summary>"Lines[1].Quantity" do livro-razão vira "items[1].quantity": a linha do ledger é o item na mesma posição.</summary>
    private static ValidationFailure ToItemField(ValidationFailure failure)
        => new(failure.PropertyName.StartsWith("Lines[") ? "Items" + failure.PropertyName["Lines".Length..] : failure.PropertyName,
            failure.ErrorMessage);

    private static Func<IQueryable<Order>, IQueryable<Order>> GetInclude()
        => q => q.Include(o => o.Store).Include(o => o.SaleLocation).Include(o => o.Client).Include(o => o.User)
            .Include(o => o.Payments)
            .Include(o => o.Items).ThenInclude(i => i.Promotion)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.Product)
            .Include(o => o.Items).ThenInclude(i => i.Variant).ThenInclude(v => v.AttributeValues)
                .ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);
}

/// <summary>
/// Prévia da venda rápida (T-VD2): o rodapé da tela (preço efetivo, totais, falta pagar e troco) calculado pela API com a
/// mesma regra da venda, sem gravar nada. Falhas de item voltam por campo; pagamento incompleto não é erro aqui.
/// </summary>
public class PreviewQuickSaleUseCase(
    IStoreRepository storeRepository,
    IStockLocationRepository locationRepository,
    IClientRepository clientRepository,
    IProductVariantRepository variantRepository,
    IProductPricingService pricing,
    IBusinessClock clock,
    IResultFactory<QuickSalePreviewDto> resultFactory) : IPreviewQuickSaleUseCase
{
    public async Task<ResultDto<QuickSalePreviewDto>> Execute(QuickSaleDto? dto, Guid userId, CancellationToken ct = default)
    {
        if (dto is null)
            return resultFactory.Failure("O corpo da requisição é obrigatório.", 400);

        var cart = await QuickSaleCart.Build(
            dto, new(storeRepository, locationRepository, clientRepository, variantRepository, pricing, clock), userId, ct);
        if (cart.Failures.Count > 0 || cart.Order is not { } order)
            return resultFactory.Failure(new ValidationResult(cart.Failures), 422);

        var paid = dto.Payments.Sum(p => p.Amount);
        var cashPaid = dto.Payments.Where(p => p.Method == PaymentMethodEnum.Cash).Sum(p => p.Amount);
        var items = order.Items.OrderBy(i => i.Position).ToList();

        return resultFactory.Success(new QuickSalePreviewDto
        {
            Items = [.. items.Select((item, index) => new QuickSalePreviewItemDto(item, cart.Promotions[index]))],
            Subtotal = order.Subtotal,
            DiscountTotal = order.DiscountTotal,
            Total = order.Total,
            Paid = paid,
            Remaining = Math.Max(order.Total - paid, 0),
            Change = dto.CashReceived is { } received && cashPaid > 0 ? Math.Max(received - cashPaid, 0) : null,
        });
    }
}
