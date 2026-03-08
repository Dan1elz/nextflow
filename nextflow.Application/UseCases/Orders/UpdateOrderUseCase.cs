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
    // Removido o IOrderItemRepository, pois manipularemos tudo via Aggregate Root (Order)
    ICreateStockMovementUseCase createStockMovement,
    IProductRepository productRepository
) : IUpdateUseCase<UpdateOrderDto, OrderResponseDto>
{
    private readonly IOrderRepository _repository = repository;
    private readonly ICreateStockMovementUseCase _createStockMovement = createStockMovement;
    private readonly IProductRepository _productRepository = productRepository;

    public async Task<OrderResponseDto> Execute(Guid id, UpdateOrderDto dto, CancellationToken ct)
    {
        dto.Validate();

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BadRequestException("O pedido deve conter pelo menos um item.");

        var entity = await _repository.GetByIdAsync(id, ct, x => x.Include(oi => oi.OrderItems).ThenInclude(p => p.Product))
            ?? throw new NotFoundException($"Pedido com id {id} não encontrado.");

        if (entity.Status != OrderStatus.PendingPayment && entity.Status != OrderStatus.Budget)
            throw new BadRequestException("Apenas pedidos orçamentos ou aguardando pagamento podem ser atualizados.");

        var requestedProductIds = dto.Items.Select(i => i.ProductId).ToHashSet(); // HashSet para buscas mais rápidas depois
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

        // 1. Atualizar existentes e adicionar novos
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
                // Item Novo
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

                // Apenas adicionamos à coleção. O EF Core cuida do resto no UpdateAsync
                entity.OrderItems.Add(newItem);

                totalAmount += newItem.UnitPrice * newItem.Quantity;
                totalDiscount += newItem.Discount;
            }
        }

        // 2. Remover itens que não vieram no DTO
        // O(1) na busca graças ao HashSet criado no início (requestedProductIds)
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

            // Apenas removemos da coleção. O EF Core vai gerar o DELETE correspondente
            entity.OrderItems.Remove(removed);
        }

        // 3. Finalização
        entity.SetTotals(totalAmount, totalDiscount);

        // Este método deve idealmente commitar as mudanças do Order e dos OrderItems no banco.
        await _repository.UpdateAsync(entity, ct);

        // IMPORTANTE: Idealmente, garantir que isso rode numa mesma transação de banco de dados
        // que o _repository.UpdateAsync, para evitar inconsistências em caso de falha.
        foreach (var movementDto in stockMovementsQueue)
        {
            await _createStockMovement.Execute(movementDto, ct);
        }

        return new OrderResponseDto(entity);
    }
}