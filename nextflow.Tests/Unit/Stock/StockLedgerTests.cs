using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Tests.Unit.Inventory;
using NSubstitute;

namespace Nextflow.Tests.Unit.Stock;

/// <summary>Regras do livro-razão com os repositórios substituídos: custo médio (RN-CE31) e estoque negativo (RN-CE32).</summary>
public class StockLedgerTests
{
    private readonly IStockBalanceRepository _balances = Substitute.For<IStockBalanceRepository>();
    private readonly IStockMovementRepository _movements = Substitute.For<IStockMovementRepository>();
    private readonly StockLocation _location = new(new LocationRequestDto { StoreId = Guid.NewGuid(), Name = "Loja" });
    private readonly List<StockMovement> _added = [];
    private readonly List<Lot> _lots = [];

    private static Product NewProduct(bool allowsNegative, bool tracksLots = false) => ProductTestData.NewProduct(new ProductRequestDto
    {
        Name = "Caderno",
        ProductType = ProductTypeEnum.Standard,
        SaleUnit = SaleUnitEnum.Unit,
        AllowsNegative = allowsNegative,
        TracksLots = tracksLots,
        ShelfLifeDays = tracksLots ? 90 : null,
        Variants = [new() { Sku = $"CAD-{Guid.NewGuid():N}"[..12], Price = 20m }],
    });

    private StockLedger Ledger(Product product, decimal companyQuantity, params StockBalance[] balances)
    {
        var variants = Substitute.For<IProductVariantRepository>();
        variants.GetAllForUpdate(Arg.Any<Expression<Func<ProductVariant, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => product.Variants.AsQueryable().Where(call.Arg<Expression<Func<ProductVariant, bool>>>()).ToList());

        var products = Substitute.For<IProductRepository>();
        products.GetAll(Arg.Any<Expression<Func<Product, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<Product>, IQueryable<Product>>?>(), Arg.Any<Func<IQueryable<Product>, IOrderedQueryable<Product>>?>(), Arg.Any<CancellationToken>())
            .Returns(new List<Product> { product });

        // O filtro de local aberto usa a navegação da loja, que só o banco preenche: aqui o local devolvido é o aberto.
        var locations = Substitute.For<IStockLocationRepository>();
        locations.GetAll(Arg.Any<Expression<Func<StockLocation, bool>>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Func<IQueryable<StockLocation>, IQueryable<StockLocation>>?>(), Arg.Any<Func<IQueryable<StockLocation>, IOrderedQueryable<StockLocation>>?>(), Arg.Any<CancellationToken>())
            .Returns(new List<StockLocation> { _location });

        _balances.GetAllForUpdate(Arg.Any<Expression<Func<StockBalance, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(_ => balances.ToList());
        _balances.SumByVariant(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, decimal> { [product.Variants.Single().Id] = companyQuantity });
        _movements.AddAsync(Arg.Do<StockMovement>(_added.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var lots = Substitute.For<ILotRepository>();
        lots.GetForVariants(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _lots.ToList());

        return new StockLedger(_balances, _movements, variants, products, locations, lots);
    }

    /// <summary>Lote da variação com saldo no local, já registrado no repositório substituído.</summary>
    private StockBalance LotBalance(Product product, string code, DateOnly expiresOn, decimal quantity)
    {
        var lot = new Lot(product.Variants.Single().Id, code, expiresOn);
        _lots.Add(lot);
        var line = Line(product, MovementTypeEnum.In, quantity);
        line.LotId = lot.Id;
        var balance = new StockBalance(line);
        balance.Apply(quantity);
        return balance;
    }

    private StockLineDto Line(Product product, MovementTypeEnum type, decimal quantity, decimal? unitCost = null) => new()
    {
        VariantId = product.Variants.Single().Id,
        LocationId = _location.Id,
        Type = type,
        Reason = type == MovementTypeEnum.In ? MovementReasonEnum.Purchase : MovementReasonEnum.Sale,
        Quantity = quantity,
        UnitCost = unitCost,
    };

    private static StockDocumentDto Document() => new() { SourceType = SourceTypeEnum.Manual, UserId = ProductTestData.UserId };

    private StockBalance Balance(Product product, decimal quantity)
    {
        var balance = new StockBalance(Line(product, MovementTypeEnum.In, quantity));
        balance.Apply(quantity);
        return balance;
    }

    [Fact]
    [Trait("Rule", "RN-CE31")]
    public async Task Should_RecalculateAverageCost_When_InboundWithCost()
    {
        var product = NewProduct(allowsNegative: false);
        var variant = product.Variants.Single();
        variant.ApplyInboundCost(0, 10, 10m);
        // 4 neste local e 6 em outro: o médio pondera pela empresa inteira, não pelo local da entrada.
        var ledger = Ledger(product, companyQuantity: 10, Balance(product, 4));

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.In, 5, unitCost: 16m)], CancellationToken.None);

        failures.ShouldBeEmpty();
        variant.AverageCost.ShouldBe(12m); // (10 × 10 + 5 × 16) / 15
        variant.LastCost.ShouldBe(16m);
        _added.Single().UnitCost.ShouldBe(16m);
        _added.Single().BalanceAfter.ShouldBe(9m);
    }

    [Fact]
    [Trait("Rule", "RN-CE31")]
    public async Task Should_KeepAverageCostAndRecordIt_When_OutboundOrInboundWithoutCost()
    {
        var product = NewProduct(allowsNegative: false);
        var variant = product.Variants.Single();
        variant.ApplyInboundCost(0, 10, 10m);
        var ledger = Ledger(product, companyQuantity: 10, Balance(product, 10));

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 3), Line(product, MovementTypeEnum.In, 2)], CancellationToken.None);

        failures.ShouldBeEmpty();
        variant.AverageCost.ShouldBe(10m);
        _added.Select(m => (m.Quantity, m.UnitCost, m.BalanceAfter))
            .ShouldBe(new (decimal, decimal?, decimal)[] { (-3m, 10m, 7m), (2m, null, 9m) });
    }

    [Theory]
    [Trait("Rule", "RN-CE31")]
    [InlineData(0)]
    [InlineData(-4)]
    public void Should_UseInboundCost_When_CompanyBalanceIsNotPositive(decimal companyQuantity)
    {
        var variant = NewProduct(allowsNegative: true).Variants.Single();
        variant.ApplyInboundCost(0, 10, 10m);

        variant.ApplyInboundCost(companyQuantity, 5, 7m);

        variant.AverageCost.ShouldBe(7m);
        variant.LastCost.ShouldBe(7m);
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Reject_When_OutboundMakesNegativeAndFlagOff()
    {
        var product = NewProduct(allowsNegative: false);
        var balance = Balance(product, 3);
        var ledger = Ledger(product, companyQuantity: 3, balance);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 5)], CancellationToken.None);

        failures.Single().PropertyName.ShouldBe("Lines[0].Quantity");
        balance.Quantity.ShouldBe(3m);
        _added.ShouldBeEmpty();
        await _balances.DidNotReceive().AddAsync(Arg.Any<StockBalance>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Reject_When_SecondLineOfTheSameDocumentMakesNegative()
    {
        var product = NewProduct(allowsNegative: false);
        var balance = Balance(product, 4);
        var ledger = Ledger(product, companyQuantity: 4, balance);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 3), Line(product, MovementTypeEnum.Out, 3)], CancellationToken.None);

        failures.Single().PropertyName.ShouldBe("Lines[1].Quantity");
        balance.Quantity.ShouldBe(4m);
        _added.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Allow_When_OutboundMakesNegativeAndFlagOn()
    {
        var product = NewProduct(allowsNegative: true);
        var balance = Balance(product, 3);
        var ledger = Ledger(product, companyQuantity: 3, balance);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 5)], CancellationToken.None);

        failures.ShouldBeEmpty();
        balance.Quantity.ShouldBe(-2m);
        _added.Single().Quantity.ShouldBe(-5m);
        _added.Single().BalanceAfter.ShouldBe(-2m);
    }

    [Fact]
    [Trait("Rule", "RN-CE10")]
    public async Task Should_Reject_When_InboundOfLotTrackedProductWithoutLot()
    {
        var product = NewProduct(allowsNegative: false, tracksLots: true);
        var ledger = Ledger(product, companyQuantity: 0);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.In, 5, unitCost: 2m)], CancellationToken.None);

        failures.Single().PropertyName.ShouldBe("Lines[0].LotId");
        failures.Single().ErrorMessage.ShouldBe(StockLedger.LotRequiredMessage);
        _added.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE10")]
    public async Task Should_Reject_When_LotGivenForProductWithoutLotTracking()
    {
        var product = NewProduct(allowsNegative: false);
        var ledger = Ledger(product, companyQuantity: 0);
        var line = Line(product, MovementTypeEnum.In, 5, unitCost: 2m);
        line.LotId = Guid.NewGuid();

        var failures = await ledger.Post(Document(), [line], CancellationToken.None);

        failures.Single().PropertyName.ShouldBe("Lines[0].LotId");
        failures.Single().ErrorMessage.ShouldBe(StockLedger.LotNotTrackedMessage);
    }

    [Fact]
    [Trait("Rule", "RN-CE15")]
    public async Task Should_SplitOutboundByEarliestExpiry_When_NoLotChosen()
    {
        var product = NewProduct(allowsNegative: false, tracksLots: true);
        var later = LotBalance(product, "L-B", new DateOnly(2027, 3, 1), 5);
        var earlier = LotBalance(product, "L-A", new DateOnly(2027, 1, 1), 3);
        var ledger = Ledger(product, companyQuantity: 8, later, earlier);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 6)], CancellationToken.None);

        failures.ShouldBeEmpty();
        _added.Select(m => (m.LotId, m.Quantity, m.BalanceAfter))
            .ShouldBe(new (Guid?, decimal, decimal)[] { (earlier.LotId, -3m, 0m), (later.LotId, -3m, 2m) });
    }

    [Fact]
    [Trait("Rule", "RN-CE32")]
    public async Task Should_Reject_When_LotsDoNotCoverOutboundAndFlagOff()
    {
        var product = NewProduct(allowsNegative: false, tracksLots: true);
        var balance = LotBalance(product, "L-A", new DateOnly(2027, 1, 1), 2);
        var ledger = Ledger(product, companyQuantity: 2, balance);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.Out, 3)], CancellationToken.None);

        failures.Single().PropertyName.ShouldBe("Lines[0].Quantity");
        balance.Quantity.ShouldBe(2m);
        _added.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE01")]
    public async Task Should_CreateBalance_When_FirstMovementOfVariantInLocation()
    {
        var product = NewProduct(allowsNegative: false);
        var ledger = Ledger(product, companyQuantity: 0);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.In, 2, unitCost: 5m)], CancellationToken.None);

        failures.ShouldBeEmpty();
        await _balances.Received(1).AddAsync(Arg.Is<StockBalance>(b => b.Quantity == 2m && b.LocationId == _location.Id), Arg.Any<CancellationToken>());
        product.Variants.Single().AverageCost.ShouldBe(5m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Should_Reject_When_QuantityIsNotPositive(decimal quantity)
    {
        var product = NewProduct(allowsNegative: true);
        var ledger = Ledger(product, companyQuantity: 0);

        var failures = await ledger.Post(Document(), [Line(product, MovementTypeEnum.In, quantity)], CancellationToken.None);

        failures.Select(f => f.PropertyName).ShouldBe(["Lines[0].Quantity"]);
        _added.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-CE14")]
    public void Should_NotUpdateNorDelete_When_UsingMovementRepository()
    {
        var methods = typeof(IStockMovementRepository).GetMethods().Select(m => m.Name).ToList();
        methods.ShouldNotContain(name => name.StartsWith("Update") || name.StartsWith("Remove"));

        IBaseRepository<StockMovement> generic = new StockMovementRepository(null!);
        Should.Throw<InvalidOperationException>(() => generic.Update(null!));
        Should.Throw<InvalidOperationException>(() => generic.UpdateRange([]));
        Should.Throw<InvalidOperationException>(() => generic.Remove(null!));
        Should.Throw<InvalidOperationException>(() => generic.RemoveRange([]));
    }
}
