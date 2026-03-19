using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Application.UseCases.Base;

namespace Nextflow.Application.UseCases.Sales;

public class CreateSaleUseCase(
    ISaleRepository repository,
    IPaymentRepository paymentRepository,
    IUpdateStatusByOrderIdUseCase updateOrderStatusByOrderIdUseCase,
    IOrderRepository orderRepository
)
    : CreateUseCaseBase<Sale, ISaleRepository, CreateSaleDto, SaleResponseDto>(repository)
{
    private readonly IPaymentRepository _paymentRepository = paymentRepository;
    private readonly IUpdateStatusByOrderIdUseCase _updateOrderStatusByOrderIdUseCase = updateOrderStatusByOrderIdUseCase;
    private readonly IOrderRepository _orderRepository = orderRepository;
    private Order? _fetchedOrder;

    protected override Sale MapToEntity(CreateSaleDto dto) => new(dto);
    protected override SaleResponseDto MapToResponseDto(Sale entity) => new(entity);

    protected override async Task ValidateBusinessRules(CreateSaleDto dto, CancellationToken ct)
    {
        _fetchedOrder = await _orderRepository.GetByIdAsync(dto.OrderId, ct)
            ?? throw new NotFoundException("Pedido não encontrado");

        var exists = await _repository.ExistsAsync(x => x.OrderId == dto.OrderId, ct);
        if (exists)
            throw new BadRequestException("Já existe uma venda para este pedido.");

        if (_fetchedOrder.Status != OrderStatus.PaymentConfirmed)
            throw new BadRequestException("A venda só pode ser publicada se o status do pedido for 'Pagamento Confirmado'.");

        var totalPayments = dto.Payments.Sum(p => p.Amount);
        if (Math.Abs(totalPayments - (double)_fetchedOrder.TotalAmount) > 0.001)
            throw new BadRequestException($"Valores divergentes: Pago {totalPayments:C} vs Pedido {_fetchedOrder.TotalAmount:C}");
    }

    protected override async Task AfterPersistence(Sale entity, CreateSaleDto dto, CancellationToken ct)
    {
        var payments = dto.Payments.Select(p =>
        {
            p.SaleId = entity.Id;
            return new Payment(p);
        }).ToList();

        await _paymentRepository.AddRangeAsync(payments, ct);

        await _updateOrderStatusByOrderIdUseCase.Execute(_fetchedOrder!.Id, dto.UserId, OrderStatus.PaymentConfirmed, "Venda finalizada", ct);
    }
}