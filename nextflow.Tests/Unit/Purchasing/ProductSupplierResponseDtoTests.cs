using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Unit.Purchasing;

public class ProductSupplierResponseDtoTests
{
    [Fact]
    [Trait("Rule", "RN-CP02")]
    public void Should_MapLastCost_When_LinkHasReceivedPurchase()
    {
        var link = new ProductSupplier(new ProductSupplierRequestDto { SupplierId = Guid.NewGuid(), LeadTimeDays = 5 }, Guid.NewGuid());

        new ProductSupplierResponseDto(link).LastCost.ShouldBeNull();

        link.RecordLastCost(7.5m, DateTime.UtcNow);

        var dto = new ProductSupplierResponseDto(link);
        dto.LastCost.ShouldBe(7.5m);
        dto.LeadTimeDays.ShouldBe(5);
    }
}
