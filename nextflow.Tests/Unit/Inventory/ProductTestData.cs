using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Models;
using NSubstitute;

namespace Nextflow.Tests.Unit.Inventory;

/// <summary>Autor e data de negócio fixos: o modelo do produto exige os dois para gravar o histórico de preço (RN-CE25).</summary>
internal static class ProductTestData
{
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly DateOnly Today = new(2026, 11, 1);

    public static IBusinessClock Clock()
    {
        var clock = Substitute.For<IBusinessClock>();
        clock.Today.Returns(Today);
        return clock;
    }

    public static Product NewProduct(ProductRequestDto dto, DateOnly? today = null) => new(dto, UserId, today ?? Today);

    public static void Edit(this Product product, ProductRequestDto dto, DateOnly? today = null) => product.Update(dto, UserId, today ?? Today);
}
