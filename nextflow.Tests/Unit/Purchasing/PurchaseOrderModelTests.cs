using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Unit.Purchasing;

public class PurchaseOrderModelTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);

    private static SavePurchaseOrderDto Dto(params (Guid VariantId, decimal Quantity, decimal UnitCost)[] items) => new()
    {
        SupplierId = Guid.NewGuid(),
        ReceiveLocationId = Guid.NewGuid(),
        Note = "  ",
        Items = [.. items.Select(i => new SavePurchaseItemDto { VariantId = i.VariantId, Quantity = i.Quantity, UnitCost = i.UnitCost })],
    };

    [Fact]
    [Trait("Rule", "RN-CE60")]
    public void Should_UseSlowestLeadTime_When_ComputingExpectedOn()
    {
        // Vínculo com prazo próprio vale; sem prazo no vínculo (ou sem vínculo) vale o do fornecedor.
        PurchaseOrder.EffectiveLeadTime(10, [3, null, 15]).ShouldBe(15);
        PurchaseOrder.EffectiveLeadTime(10, [3, 4]).ShouldBe(4);
        PurchaseOrder.EffectiveLeadTime(10, [3, null]).ShouldBe(10);
        PurchaseOrder.EffectiveLeadTime(10, []).ShouldBe(10);

        var order = new PurchaseOrder(Dto((Guid.NewGuid(), 3, 2.335m)), Guid.NewGuid(), Guid.NewGuid(), leadTimeDays: 15, Today);

        order.Status.ShouldBe(PurchaseStatusEnum.Budget);
        order.ExpectedOn.ShouldBe(Today.AddDays(15));
        order.Note.ShouldBeNull();
        order.Total.ShouldBe(7.01m);

        // Enviar recomeça a contagem do dia do envio (RN-CP08).
        order.Send(DateTime.UtcNow, Today.AddDays(2));
        order.Status.ShouldBe(PurchaseStatusEnum.Pending);
        order.SentAt.ShouldNotBeNull();
        order.ExpectedOn.ShouldBe(Today.AddDays(17));
    }
}
