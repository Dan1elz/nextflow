using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;
using Nextflow.Domain.Interfaces.UseCases.Base;

namespace Nextflow.Application.UseCases.Orders;

public class UpdateOrderUseCase(
    IOrderRepository repository,
    IOrderItemRepository orderItemRepository,
    ICreateStockMovementUseCase createStockMovement,
    IProductRepository productRepository
) : IUpdateUseCase<UpdateOrderDto, OrderResponseDto>
{
    private readonly IOrderRepository _repository = repository;
    private readonly IOrderItemRepository _orderItemRepository = orderItemRepository;
    private readonly ICreateStockMovementUseCase _createStockMovement = createStockMovement;
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<OrderResponseDto> Execute(Guid id, UpdateOrderDto dto, CancellationToken ct)
    {
        dto.Validate();

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BadRequestException("O pedido deve conter pelo menos um item.");

        var entity = await _repository.GetByIdAsync(id, ct, x => x.Include(oi => oi.OrderItems))
            ?? throw new NotFoundException($"Pedido com id {id} não encontrado.");

        if (entity.Status != OrderStatus.PendingPayment && entity.Status != OrderStatus.Budget)
            throw new BadRequestException("Apenas pedidos orçamentos ou aguardando pagamento podem ser atualizados.");

        var requestedProductIds = dto.Items.Select(i => i.ProductId).ToHashSet();
        var products = await _productRepository.GetAllAsync(p => requestedProductIds.Contains(p.Id), 0, int.MaxValue, ct);

        if (products.Count() != requestedProductIds.Count)
            throw new BadRequestException("Um ou mais produtos não foram encontrados.");

        var productMap = products.ToDictionary(p => p.Id);

        // Validação de Estoque
        foreach (var item in dto.Items)
        {
            var product = productMap[item.ProductId];

            if (item.Quantity <= 0)
                throw new BadRequestException($"Quantidade inválida para o produto {product.Name}. A quantidade deve ser maior que zero.");

            if (entity.Type != OrderType.Budget)
            {
                var existingQty = entity.OrderItems.FirstOrDefault(oi => oi.ProductId == item.ProductId)?.Quantity ?? 0;
                var additionalRequested = item.Quantity - existingQty;

                if (additionalRequested > 0 && additionalRequested > product.Quantity)
                    throw new BadRequestException($"Estoque insuficiente para o produto {product.Name}.");
            }
        }

        var existingItemsMap = entity.OrderItems.ToDictionary(i => i.ProductId);
        var stockMovementsQueue = new List<CreateStockMovementDto>();

        decimal totalAmount = 0;
        decimal totalDiscount = 0;

        // Lists to track additions and removals (processed AFTER save to avoid tracker conflicts)
        var itemsToAdd = new List<OrderItem>();
        var itemsToRemove = new List<OrderItem>();

        // 1. Update existing items and prepare new items
        foreach (var itemDto in dto.Items)
        {
            var product = productMap[itemDto.ProductId];

            if (existingItemsMap.TryGetValue(itemDto.ProductId, out OrderItem? existingItem))
            {
                if (existingItem.Quantity != itemDto.Quantity || existingItem.Discount != itemDto.Discount)
                {
                    if (entity.Type != OrderType.Budget && existingItem.Quantity != itemDto.Quantity)
                    {
                        var quantityDifference = itemDto.Quantity - existingItem.Quantity;
                        var movementType = quantityDifference > 0 ? MovementType.Sales : MovementType.Return;

                        stockMovementsQueue.Add(new CreateStockMovementDto
                        {
                            ProductId = itemDto.ProductId,
                            Quantity = (double)Math.Abs(quantityDifference),
                            MovementType = movementType,
                            Description = $"Ajuste (Update) do pedido {entity.Id}",
                            UserId = dto.UserId,
                            Quote = product.Price,
                            IsSystemGenerated = true
                        });
                    }

                    existingItem.UpdateDetails(itemDto.Quantity, itemDto.Discount);
                    existingItem.SetPricing(product.Price);
                }

                totalAmount += existingItem.UnitPrice * existingItem.Quantity;
                totalDiscount += existingItem.Discount;
            }
            else
            {
                // New item - will be added separately via repository
                itemDto.OrderId = entity.Id;
                var newItem = new OrderItem(itemDto);
                newItem.SetPricing(product.Price);

                if (entity.Type != OrderType.Budget)
                {
                    stockMovementsQueue.Add(new CreateStockMovementDto
                    {
                        ProductId = itemDto.ProductId,
                        Quantity = (double)itemDto.Quantity,
                        MovementType = MovementType.Sales,
                        Description = $"Adição (Update) ao pedido {entity.Id}",
                        UserId = dto.UserId,
                        Quote = product.Price,
                        IsSystemGenerated = true
                    });
                }

                itemsToAdd.Add(newItem);

                totalAmount += newItem.UnitPrice * newItem.Quantity;
                totalDiscount += newItem.Discount;
            }
        }

        // 2. Identify items to remove
        var removedItems = existingItemsMap.Values
            .Where(i => !requestedProductIds.Contains(i.ProductId))
            .ToList();

        foreach (var removed in removedItems)
        {
            if (entity.Type != OrderType.Budget)
            {
                stockMovementsQueue.Add(new CreateStockMovementDto
                {
                    ProductId = removed.ProductId,
                    Quantity = (double)removed.Quantity,
                    MovementType = MovementType.Return,
                    Description = $"Remoção (Update) do pedido {entity.Id}",
                    UserId = dto.UserId,
                    Quote = removed.UnitPrice,
                    IsSystemGenerated = true
                });
            }

            itemsToRemove.Add(removed);
        }

        // 3. Finalize
        entity.SetTotals(totalAmount, totalDiscount);

        // Save existing item modifications + order totals (tracked entities only)
        await _repository.SaveAsync(ct);

        // 4. Remove items via dedicated repository (separate SaveChanges)
        if (itemsToRemove.Count > 0)
        {
            await _orderItemRepository.RemoveRangeAsync(itemsToRemove, ct);
        }

        // 5. Add new items via dedicated repository (separate SaveChanges)
        if (itemsToAdd.Count > 0)
        {
            await _orderItemRepository.AddRangeAsync(itemsToAdd, ct);
        }

        // 6. Process stock movements
        foreach (var movementDto in stockMovementsQueue)
        {
            await _createStockMovement.Execute(movementDto, ct);
        }

        // Reload to return updated data
        var updated = await _repository.GetByIdAsync(id, ct, x => x.Include(oi => oi.OrderItems))
            ?? throw new NotFoundException($"Pedido com id {id} não encontrado.");

        return new OrderResponseDto(updated);
    }
}