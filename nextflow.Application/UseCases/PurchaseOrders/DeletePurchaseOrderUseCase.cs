using Microsoft.EntityFrameworkCore;
using Nextflow.Application.UseCases.Base;
using Nextflow.Domain.Exceptions;
using Nextflow.Domain.Interfaces.Repositories;
using Nextflow.Domain.Interfaces.UseCases;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.PurchaseOrders;

public class DeletePurchaseOrderUseCase(
    IPurchaseOrderRepository repository,
    IPurchaseItemRepository itemRepository
) : DeleteUseCaseBase<PurchaseOrder, IPurchaseOrderRepository>(repository)
{
    private readonly IPurchaseItemRepository _itemRepository = itemRepository;

    protected override Func<IQueryable<PurchaseOrder>, IQueryable<PurchaseOrder>>? GetInclude() =>
        query => query.Include(p => p.PurchaseItems);

    protected override async Task PerformSideEffects(PurchaseOrder entity, CancellationToken ct, Guid? userId = null)
    {
        // Absolute deletion of items
        if (entity.PurchaseItems.Count != 0)
        {
            await _itemRepository.RemoveRangeAsync(entity.PurchaseItems, ct);
        }
    }
}
