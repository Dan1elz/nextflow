using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.PurchaseOrders;

public class GetPurchaseOrderByIdUseCase(IPurchaseOrderRepository repository)
    : GetByIdUseCaseBase<PurchaseOrder, IPurchaseOrderRepository, PurchaseOrderResponseDto>(repository)
{
    protected override PurchaseOrderResponseDto MapToResponseDto(PurchaseOrder entity) => new(entity);

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>>? GetInclude() =>
        query => query.Include(p => p.Supplier).Include(p => p.PurchaseItems).ThenInclude(i => i.Product);
}
