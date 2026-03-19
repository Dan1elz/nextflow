using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.PurchaseOrders;

public class CreatePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IProductRepository productRepository,
    ISupplierRepository supplierRepository
)
    : CreateUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, CreatePurchaseOrderDto, PurchaseOrderResponseDto>(repository)
{
    private readonly IProductRepository _productRepository = productRepository;
    private readonly ISupplierRepository _supplierRepository = supplierRepository;

    protected override PurchaseOrder MapToEntity(CreatePurchaseOrderDto dto) => new(dto.SupplierId, dto.Note);
    protected override PurchaseOrderResponseDto MapToResponseDto(PurchaseOrder entity) => new(entity);

    protected override async Task ValidateBusinessRules(CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        var supplierExists = await _supplierRepository.ExistsAsync(s => s.Id == dto.SupplierId, ct);
        if (!supplierExists)
            throw new NotFoundException("Fornecedor não encontrado.");

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BadRequestException("O pedido de compra deve conter pelo menos um item.");

        var productsId = dto.Items.Select(i => i.ProductId).Distinct().ToList();
        var foundProductsCount = await _productRepository.CountAsync(p => productsId.Contains(p.Id), ct);

        if (foundProductsCount != productsId.Count)
            throw new BadRequestException("Um ou mais produtos não foram encontrados.");
    }

    protected override Task BeforePersistence(PurchaseOrder entity, CreatePurchaseOrderDto dto, CancellationToken ct)
    {
        decimal totalAmount = 0;

        foreach (var itemDto in dto.Items)
        {
            var item = new PurchaseItem(
                entity.Id,
                itemDto.ProductId,
                itemDto.Quantity,
                itemDto.Discount,
                itemDto.CostPrice
            );

            // TotalAmount = (Quantity * Cost) - Discount
            totalAmount += ((decimal)item.Quantity * item.CostPrice) - item.Discount;

            entity.PurchaseItems.Add(item);
        }

        entity.SetTotalAmount(totalAmount);

        return Task.CompletedTask;
    }
}
