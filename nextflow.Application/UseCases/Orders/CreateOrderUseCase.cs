using Nextflow.Application.UseCases.Base;
using Nextflow.Application.Utils;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Interfaces.Utils;
using Nextflow.Domain.Models;
using Nextflow.Domain.Utils;

namespace Nextflow.Application.UseCases.Orders;
public class CreateOrderUseCase(
    IOrderRepository repository,
    IProductRepository productRepository,
    ICreateStockMovementUseCase createStockMovement,
    IProductPricingService pricingService
)
    : CreateUseCaseBase<Order, IOrderRepository, CreateOrderDto, OrderResponseDto>(repository), ICreateOrderUseCase
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ICreateStockMovementUseCase _createStockMovement = createStockMovement;
    private readonly IProductPricingService _pricingService = pricingService;

    private Dictionary<Guid, Product>? _productMap;
    private DateOnly _pricingDate;

    protected override Order MapToEntity(CreateOrderDto dto) => new(dto);
    protected override OrderResponseDto MapToResponseDto(Order entity) => new(entity);

    protected override async Task ValidateBusinessRules(CreateOrderDto dto, CancellationToken ct)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new BadRequestException("O pedido deve conter pelo menos um item.");

        var productsId = dto.Items.Select(i => i.ProductId).Distinct().ToList();

        _pricingDate = BusinessClock.Today;
        var pricingDate = _pricingDate;
        var products = (await _productRepository.GetAllAsync(p => productsId.Contains(p.Id), 0, int.MaxValue, ct,
            query => query.IncludeCurrentPromotions(pricingDate))).ToList();

        if (products.Count != productsId.Count)
            throw new BadRequestException("Um ou mais produtos não foram encontrados.");

        _productMap = products.ToDictionary(p => p.Id);

        foreach (var item in dto.Items)
        {
            if (!_productMap.TryGetValue(item.ProductId, out var product))
                throw new BadRequestException($"Produto com ID {item.ProductId} não encontrado.");

            if (item.Quantity <= 0)
                throw new BadRequestException($"Quantidade inválida para o produto {product.Name}. A quantidade deve ser maior que zero.");

            if (dto.Type != OrderType.Budget && item.Quantity > product.Quantity)
                throw new BadRequestException($"Estoque insuficiente para o produto {product.Name}.");
        }
    }

    protected override Task BeforePersistence(Order entity, CreateOrderDto dto, CancellationToken ct)
    {
        decimal totalAmount = 0;
        decimal totalDiscount = 0;

        foreach (var itemDto in dto.Items)
        {
            var product = _productMap![itemDto.ProductId];

            itemDto.OrderId = entity.Id;
            var orderItem = new OrderItem(itemDto);

            // Preço efetivo: aplica a promoção vigente (se houver)
            var unitPrice = _pricingService.GetEffectivePrice(product, _pricingDate);
            var totalPrice = unitPrice * itemDto.Quantity;

            totalAmount += totalPrice;
            totalDiscount += itemDto.Discount;

            orderItem.SetPricing(unitPrice);

            entity.OrderItems.Add(orderItem);
        }

        entity.SetTotals(totalAmount, totalDiscount);

        return Task.CompletedTask;
    }

    protected override async Task AfterPersistence(Order entity, CreateOrderDto dto, CancellationToken ct)
    {
        if (entity.Type == OrderType.Budget)
            return;

        foreach (var item in entity.OrderItems)
        {
            await _createStockMovement.Execute(new CreateStockMovementDto
            {
                ProductId = item.ProductId,
                Quantity = (double)item.Quantity,
                MovementType = MovementType.Sales,
                Description = $"Movimentação de estoque para o pedido {entity.Id}",
                UserId = dto.UserId,
                Quote = item.UnitPrice,
                IsSystemGenerated = true
            }, ct);
        }
    }
}