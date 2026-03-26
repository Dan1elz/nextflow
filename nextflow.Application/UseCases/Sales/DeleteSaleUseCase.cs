using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;

public class DeleteSaleUseCase(
    ISaleRepository repository,
    IPaymentRepository paymentRepository,
    IUpdateStatusByOrderIdUseCase updateOrderStatus
    ) : DeleteUseCaseBase<Sale, ISaleRepository>(repository), IDeleteSaleUseCase
{
    protected override Func<IQueryable<Sale>, IQueryable<Sale>>? GetInclude()
    {
        return q => q.Include(s => s.Order);
    }

    protected override void ValidateBusinessRules(Sale entity)
    {
        if (entity.Order == null)
            throw new BadRequestException("Venda sem pedido associado.");

        var timeDiff = DateTime.UtcNow - entity.Order.CreateAt;
        if (timeDiff.TotalHours > 48)
            throw new BadRequestException("A venda só pode ser excluída se o pedido foi criado em menos de 48 horas.");
    }

    protected override async Task PerformSideEffects(Sale entity, CancellationToken ct, Guid? userId)
    {
        // Buscar pagamentos vinculados para deleção absoluta
        var payments = await paymentRepository.GetAllAsync(p => p.SaleId == entity.Id, 0, 1000, ct);
        if (payments.Any())
        {
            await paymentRepository.RemoveRangeAsync(payments, ct);
        }

        if (userId.HasValue)
            await updateOrderStatus.Execute(entity.OrderId, userId!.Value, OrderStatus.Refunded, "Venda reembolsada e excluída", ct);
    }
}