using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;


public class DeleteSaleUseCase(
    ISaleRepository repository,
    IPaymentRepository paymentRepository,
    IUpdateStatusByOrderIdUseCase updateOrderStatus
    ) : DeleteUseCaseBase<Sale, ISaleRepository>(repository)
{
    protected override Func<IQueryable<Sale>, IQueryable<Sale>>? GetInclude()
    {
        return q => q.Include(s => s.Payments);
    }
    protected override async Task PerformSideEffects(Sale entity, CancellationToken ct, Guid? userId)
    {
        foreach (var p in entity.Payments) p.Delete();
        await paymentRepository.UpdateRangeAsync(entity.Payments, ct);
        if (userId.HasValue)
            await updateOrderStatus.Execute(entity.OrderId, userId!.Value, OrderStatus.Refunded, "Venda estornada", ct);
    }
}