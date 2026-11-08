using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Dtos.Forecasts;
using Nextflow.Domain.Dtos.PurchaseOrders;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Tests.Support;

namespace Nextflow.Tests.Integration;

/// <summary>
/// Ações sobre as recomendações (F3.4) num Postgres real: seed demo de 14 meses, relógio parado em 26/10/2026, uma análise rodada
/// com a IA fake. Os use cases são chamados direto do contêiner de DI (o que o controller faria).
/// </summary>
[Collection(DemoSeedCollection.Name)]
public class ForecastActionsTests(DemoSeedFixture fixture)
{
    private static readonly DateOnly Anchor = new(2026, 10, 26);

    private sealed record Seeded(string Connection, IServiceProvider Services, Guid AdminId, Guid RunId);

    private static Seeded? _seeded;

    private async Task<Seeded> Seed()
    {
        if (_seeded is not null)
            return _seeded;

        var ai = new FakeAiClient();
        var connection = await fixture.NewDatabaseAsync();
        var services = fixture.StartApi(connection, demoEnabled: true, historyMonths: 14, configureServices: s =>
        {
            s.AddSingleton<IBusinessClock>(new FixedBusinessClock(Anchor));
            s.AddSingleton<IAiClient>(ai);
        });

        using var scope = services.CreateScope();
        var admin = await scope.ServiceProvider.GetRequiredService<IUserRepository>().Get(u => u.Email == AccessApiFixture.AdminEmail);
        var clock = scope.ServiceProvider.GetRequiredService<IBusinessClock>();
        var run = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Manual, admin!.Id), clock.Today, clock.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IForecastRunRepository>().AddAsync(run);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
        await scope.ServiceProvider.GetRequiredService<IForecastEngine>().RunAsync(run.Id, CancellationToken.None);

        return _seeded = new Seeded(connection, services, admin.Id, run.Id);
    }

    private static T Use<T>(IServiceScope scope) where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Recomendações pendentes de uma ação, com produto, fornecedores e itens, que nenhum outro teste usou.</summary>
    private async Task<List<ForecastRecommendation>> Pending(Seeded seeded, RecommendationActionEnum action)
    {
        await using var context = fixture.CreateContext(seeded.Connection);
        return await context.Set<ForecastRecommendation>()
            .Include(r => r.Items)
            .Include(r => r.Product).ThenInclude(p => p.Suppliers)
            .Where(r => r.ForecastRunId == seeded.RunId && r.Action == action && r.Status == RecommendationStatusEnum.Pending)
            .OrderBy(r => r.Product.Name)
            .ToListAsync();
    }

    private async Task<Guid> ReceivingLocation(Seeded seeded)
    {
        await using var context = fixture.CreateContext(seeded.Connection);
        return await context.Set<StockLocation>().Where(l => l.IsActive).OrderBy(l => l.Name).Select(l => l.Id).FirstAsync();
    }

    private static ApplyPurchaseGroupDto Group(Guid supplierId, Guid locationId, params ForecastRecommendation[] recommendations) => new()
    {
        SupplierId = supplierId,
        ReceiveLocationId = locationId,
        Items = [.. recommendations.SelectMany(r => r.Items.Where(i => i.Quantity > 0).Select(i => new ApplyPurchaseItemDto
        {
            RecommendationId = r.Id,
            VariantId = i.VariantId,
            Quantity = i.Quantity,
            UnitCost = 5m,
        }))],
    };

    [Fact]
    [Trait("Rule", "RN-DF15")]
    public async Task Should_CreateBudgetPurchaseOrderPerSupplier_When_ApplyingBuy()
    {
        var seeded = await Seed();
        var location = await ReceivingLocation(seeded);
        var buys = (await Pending(seeded, RecommendationActionEnum.Buy)).Where(r => r.Product.Suppliers.Any(s => s.IsPreferred)).ToList();
        var bySupplier = buys.GroupBy(r => r.Product.Suppliers.First(s => s.IsPreferred).SupplierId).Take(2).ToList();
        bySupplier.ShouldNotBeEmpty();
        var groups = bySupplier.Select(g => Group(g.Key, location, g.First())).ToList();

        using var scope = seeded.Services.CreateScope();
        var result = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto { Groups = groups }, seeded.AdminId);

        result.IsSuccess.ShouldBeTrue(result.Message);
        result.Data!.PurchaseOrders.Count.ShouldBe(groups.Count);
        result.Data.Recommendations.ShouldAllBe(r => r.Status == "Applied" && r.AppliedLink != null && r.AppliedLink.Type == "PurchaseOrder");

        await using var context = fixture.CreateContext(seeded.Connection);
        var ids = result.Data.PurchaseOrders.Select(p => p.Id).ToList();
        var orders = await context.Set<PurchaseOrder>().Include(p => p.Items).Where(p => ids.Contains(p.Id)).ToListAsync();
        orders.ShouldAllBe(p => p.Status == PurchaseStatusEnum.Budget && p.SentAt == null && p.Number > 0); // nada é enviado sozinho
        foreach (var group in groups)
        {
            var order = orders.Single(o => o.SupplierId == group.SupplierId);
            order.Items.Sum(i => i.Quantity).ShouldBe(group.Items.Sum(i => i.Quantity));
            var recommendation = await context.Set<ForecastRecommendation>().SingleAsync(r => r.Id == group.Items[0].RecommendationId);
            recommendation.Status.ShouldBe(RecommendationStatusEnum.Applied);
            recommendation.AppliedPurchaseOrderId.ShouldBe(order.Id);
            recommendation.AppliedById.ShouldBe(seeded.AdminId);
        }
    }

    [Fact]
    [Trait("Rule", "F3.4")]
    public async Task Should_ReturnRecommendationToPending_When_GeneratedPurchaseIsCanceled()
    {
        var seeded = await Seed();
        var location = await ReceivingLocation(seeded);
        var buy = (await Pending(seeded, RecommendationActionEnum.Buy)).Last(r => r.Product.Suppliers.Any(s => s.IsPreferred));
        var supplierId = buy.Product.Suppliers.First(s => s.IsPreferred).SupplierId;

        Guid orderId;
        using (var scope = seeded.Services.CreateScope())
        {
            var applied = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto { Groups = [Group(supplierId, location, buy)] }, seeded.AdminId);
            applied.IsSuccess.ShouldBeTrue(applied.Message);
            orderId = applied.Data!.PurchaseOrders[0].Id;
        }

        uint version;
        await using (var context = fixture.CreateContext(seeded.Connection))
            version = (await context.Set<PurchaseOrder>().SingleAsync(p => p.Id == orderId)).Version;

        using (var scope = seeded.Services.CreateScope())
        {
            var canceled = await Use<ICancelPurchaseOrderUseCase>(scope).Execute(orderId, new CancelPurchaseOrderDto { Version = version, Reason = "Comprei de outro jeito." });
            canceled.IsSuccess.ShouldBeTrue(canceled.Message);
        }

        await using var check = fixture.CreateContext(seeded.Connection);
        var recommendation = await check.Set<ForecastRecommendation>().SingleAsync(r => r.Id == buy.Id);
        recommendation.Status.ShouldBe(RecommendationStatusEnum.Pending);
        recommendation.AppliedPurchaseOrderId.ShouldBeNull();
    }

    /// <summary>
    /// Aplica uma "Comprar" dividida entre as duas lojas (RN-DF27): pega a sugestão da API (que tem de somar a quantidade de cada
    /// variação) e manda um grupo por loja, cada um no local de recebimento dela, com metade da quantidade de cada variação.
    /// </summary>
    private async Task<(ForecastRecommendation Buy, List<Guid> OrderIds)> ApplySplit(Seeded seeded)
    {
        var buy = (await Pending(seeded, RecommendationActionEnum.Buy))
            .First(r => r.Product.Suppliers.Any(s => s.IsPreferred) && r.Items.Any(i => i.Quantity >= 2));
        var supplierId = buy.Product.Suppliers.First(s => s.IsPreferred).SupplierId;

        using var scope = seeded.Services.CreateScope();
        var split = await Use<IGetRecommendationStoreSplitUseCase>(scope).Execute(buy.Id);
        split.IsSuccess.ShouldBeTrue(split.Message);
        split.Data!.Stores.Count.ShouldBe(2);
        split.Data.Items.ShouldAllBe(i => i.ByStore.Sum(p => p.Quantity) == i.Quantity);
        split.Data.Stores.Sum(s => s.Quantity).ShouldBe(buy.Items.Sum(i => i.Quantity));

        var items = buy.Items.Where(i => i.Quantity >= 2).ToList();
        var groups = split.Data.Stores.Select((store, index) => new ApplyPurchaseGroupDto
        {
            SupplierId = supplierId,
            ReceiveLocationId = store.ReceiveLocationId!.Value,
            Items = [.. items.Select(i => new ApplyPurchaseItemDto
            {
                RecommendationId = buy.Id,
                VariantId = i.VariantId,
                Quantity = index == 0 ? Math.Ceiling(i.Quantity / 2) : i.Quantity - Math.Ceiling(i.Quantity / 2),
                UnitCost = 5m,
            })],
        }).ToList();

        var applied = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto { Groups = groups }, seeded.AdminId);
        applied.IsSuccess.ShouldBeTrue(applied.Message);
        return (buy, [.. applied.Data!.PurchaseOrders.Select(p => p.Id)]);
    }

    private async Task Cancel(Seeded seeded, Guid orderId)
    {
        uint version;
        await using (var context = fixture.CreateContext(seeded.Connection))
            version = (await context.Set<PurchaseOrder>().SingleAsync(p => p.Id == orderId)).Version;
        using var scope = seeded.Services.CreateScope();
        var canceled = await Use<ICancelPurchaseOrderUseCase>(scope).Execute(orderId, new CancelPurchaseOrderDto { Version = version, Reason = "Teste." });
        canceled.IsSuccess.ShouldBeTrue(canceled.Message);
    }

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public async Task Should_CreateOneOrderPerStore_When_BuySplitAcrossStores()
    {
        var seeded = await Seed();
        var (buy, orderIds) = await ApplySplit(seeded);

        orderIds.Count.ShouldBe(2);
        await using var context = fixture.CreateContext(seeded.Connection);
        var orders = await context.Set<PurchaseOrder>().Where(p => orderIds.Contains(p.Id)).ToListAsync();
        orders.Select(o => o.StoreId).Distinct().Count().ShouldBe(2);
        orders.ShouldAllBe(o => o.Status == PurchaseStatusEnum.Budget);

        var recommendation = await context.Set<ForecastRecommendation>().Include(r => r.PurchaseOrders).SingleAsync(r => r.Id == buy.Id);
        recommendation.Status.ShouldBe(RecommendationStatusEnum.Applied);
        recommendation.PurchaseOrders.Select(l => l.PurchaseOrderId).ShouldBe(orderIds, ignoreOrder: true);
        orderIds.ShouldContain(recommendation.AppliedPurchaseOrderId!.Value);
    }

    [Fact]
    [Trait("Rule", "RN-DF28")]
    public async Task Should_KeepApplied_When_OnlyOneOfSplitOrdersIsCanceled()
    {
        var seeded = await Seed();
        var (buy, orderIds) = await ApplySplit(seeded);
        await using (var before = fixture.CreateContext(seeded.Connection))
        {
            var first = (await before.Set<ForecastRecommendation>().SingleAsync(r => r.Id == buy.Id)).AppliedPurchaseOrderId!.Value;
            await Cancel(seeded, first);
        }

        await using var context = fixture.CreateContext(seeded.Connection);
        var recommendation = await context.Set<ForecastRecommendation>().SingleAsync(r => r.Id == buy.Id);
        recommendation.Status.ShouldBe(RecommendationStatusEnum.Applied);
        var open = await context.Set<PurchaseOrder>().SingleAsync(p => orderIds.Contains(p.Id) && p.Status != PurchaseStatusEnum.Canceled);
        recommendation.AppliedPurchaseOrderId.ShouldBe(open.Id);
    }

    [Fact]
    [Trait("Rule", "RN-DF28")]
    public async Task Should_ReturnToPending_When_AllSplitOrdersAreCanceled()
    {
        var seeded = await Seed();
        var (buy, orderIds) = await ApplySplit(seeded);
        foreach (var orderId in orderIds)
            await Cancel(seeded, orderId);

        await using var context = fixture.CreateContext(seeded.Connection);
        var recommendation = await context.Set<ForecastRecommendation>().SingleAsync(r => r.Id == buy.Id);
        recommendation.Status.ShouldBe(RecommendationStatusEnum.Pending);
        recommendation.AppliedPurchaseOrderId.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-DF27")]
    public async Task Should_SplitBuyAcrossStores_When_AnalysisRuns()
    {
        var seeded = await Seed();
        await using var context = fixture.CreateContext(seeded.Connection);

        var buys = await context.Set<ForecastRecommendation>().Include(r => r.Stores)
            .Where(r => r.ForecastRunId == seeded.RunId && r.Action == RecommendationActionEnum.Buy && r.SuggestedQuantity > 0)
            .ToListAsync();

        buys.ShouldNotBeEmpty();
        buys.ShouldAllBe(r => r.Stores.Count == 2 && r.Stores.Sum(s => s.Quantity) == r.SuggestedQuantity);
        buys.ShouldAllBe(r => r.Reason.Contains("Por loja:"));
    }

    [Fact]
    [Trait("Rule", "RN-DF16")]
    public async Task Should_GenerateTransferRecommendation_When_DemoScenarioSeeded()
    {
        var seeded = await Seed();

        var transfers = await Pending(seeded, RecommendationActionEnum.Transfer);

        transfers.ShouldNotBeEmpty();
        transfers.ShouldAllBe(r => r.TransferFromLocationId != null && r.TransferToLocationId != null
            && r.TransferFromLocationId != r.TransferToLocationId && r.Items.Sum(i => i.Quantity) > 0 && r.SuggestedQuantity > 0);
    }

    [Fact]
    [Trait("Rule", "RN-CE62")]
    public async Task Should_CreateTransfer_When_ApplyingTransferRecommendation()
    {
        var seeded = await Seed();
        var transfer = (await Pending(seeded, RecommendationActionEnum.Transfer)).First();
        var to = transfer.TransferToLocationId!.Value;
        var variantIds = transfer.Items.Select(i => i.VariantId).ToList();

        decimal before;
        await using (var context = fixture.CreateContext(seeded.Connection))
            before = await context.Set<StockBalance>().Where(b => b.LocationId == to && variantIds.Contains(b.VariantId)).SumAsync(b => b.Quantity);

        using (var scope = seeded.Services.CreateScope())
        {
            var applied = await Use<IApplyTransferRecommendationUseCase>(scope).Execute(transfer.Id, null, seeded.AdminId);
            applied.IsSuccess.ShouldBeTrue(applied.Message);
            applied.Data!.Status.ShouldBe("Applied");
            applied.Data.AppliedLink!.Type.ShouldBe("Transfer");
        }

        await using var check = fixture.CreateContext(seeded.Connection);
        var recommendation = await check.Set<ForecastRecommendation>().SingleAsync(r => r.Id == transfer.Id);
        recommendation.Status.ShouldBe(RecommendationStatusEnum.Applied);
        var created = await check.Set<StockTransfer>().Include(t => t.Items).SingleAsync(t => t.Id == recommendation.AppliedTransferId);
        created.ForecastRecommendationId.ShouldBe(transfer.Id);
        created.ToLocationId.ShouldBe(to);
        var after = await check.Set<StockBalance>().Where(b => b.LocationId == to && variantIds.Contains(b.VariantId)).SumAsync(b => b.Quantity);
        after.ShouldBe(before + transfer.Items.Sum(i => i.Quantity));
    }

    [Fact]
    [Trait("Rule", "RN-DF15")]
    public async Task Should_RejectPurchase_When_RecommendationIsNotPendingBuy()
    {
        var seeded = await Seed();
        var location = await ReceivingLocation(seeded);
        var hold = (await Pending(seeded, RecommendationActionEnum.Hold)).First();
        await using var context = fixture.CreateContext(seeded.Connection);
        var variantId = await context.Set<ProductVariant>().Where(v => v.ProductId == hold.ProductId).Select(v => v.Id).FirstAsync();
        var supplierId = await context.Set<Supplier>().Where(s => s.IsActive).Select(s => s.Id).FirstAsync();

        using var scope = seeded.Services.CreateScope();
        var result = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto
        {
            Groups = [new() { SupplierId = supplierId, ReceiveLocationId = location, Items = [new() { RecommendationId = hold.Id, VariantId = variantId, Quantity = 1, UnitCost = 1 }] }],
        }, seeded.AdminId);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.Keys.ShouldContain(k => k.EndsWith("recommendationId", StringComparison.OrdinalIgnoreCase));
        (await context.Set<PurchaseOrder>().CountAsync(p => p.CreatedById == seeded.AdminId && p.Status == PurchaseStatusEnum.Budget && p.Items.Any(i => i.VariantId == variantId))).ShouldBe(0);
    }

    [Fact]
    [Trait("Rule", "RN-DF07")]
    public async Task Should_CreatePromotionLinkedToRecommendation_When_ApplyingPromote()
    {
        var seeded = await Seed();
        Guid recommendationId;
        // O seed não garante um "Promover": monta uma análise concluída só com ele, como o motor gravaria.
        using (var scope = seeded.Services.CreateScope())
        {
            await using var context = fixture.CreateContext(seeded.Connection);
            var product = await context.Set<Product>()
                .Where(p => p.IsActive && !p.Promotions.Any(promo => promo.IsActive))
                .OrderBy(p => p.Name).FirstAsync();
            var clock = Use<IBusinessClock>(scope);
            var run = new ForecastRun(new StartForecastRunDto(ForecastTriggerEnum.Manual, seeded.AdminId), clock.Today, clock.UtcNow);
            run.Complete(ForecastRunStatusEnum.CompletedWithoutAi, "Teste.", null, null, null, clock.UtcNow);
            var recommendation = new ForecastRecommendation(new NewForecastRecommendationDto(
                run.Id, product.Id, RecommendationActionEnum.Promote, RecommendationPriorityEnum.Medium, ForecastConfidenceEnum.High,
                null, null, 15m, 21, null, "Excesso em baixa temporada.", "Excesso em baixa temporada.", false,
                100, 0, 30, 10, 300, 0.5m, 3, 10, LeadTimeSourceEnum.Supplier, 14, false, []));
            await Use<IForecastRunRepository>(scope).AddAsync(run);
            await Use<IForecastRecommendationRepository>(scope).AddAsync(recommendation);
            await Use<IUnitOfWork>(scope).CommitAsync();
            recommendationId = recommendation.Id;
        }

        using (var scope = seeded.Services.CreateScope())
        {
            var result = await Use<IApplyPromotionRecommendationUseCase>(scope).Execute(recommendationId, new ApplyPromotionDto
            {
                DiscountPercent = 10,
                StartDate = Anchor,
                EndDate = Anchor.AddDays(20),
                ConfirmWarnings = true,
            }, seeded.AdminId);

            result.IsSuccess.ShouldBeTrue(result.Message);
            result.Data!.PromotionIds.Count.ShouldBe(1);
            result.Data.Recommendation.Status.ShouldBe("Applied");
            result.Data.Recommendation.AppliedLink!.Type.ShouldBe("Promotion");

            // Aplicar de novo: já não está pendente.
            (await Use<IApplyPromotionRecommendationUseCase>(scope).Execute(recommendationId, new ApplyPromotionDto
            {
                DiscountPercent = 10,
                StartDate = Anchor,
                EndDate = Anchor.AddDays(20),
            }, seeded.AdminId)).ErrorCode.ShouldBe(400);
        }

        await using var check = fixture.CreateContext(seeded.Connection);
        var promotion = await check.Set<Promotion>().SingleAsync(p => p.ForecastRecommendationId == recommendationId);
        promotion.Source.ShouldBe(PromotionSourceEnum.AiRecommendation);
        promotion.VariantId.ShouldBeNull();
        promotion.DiscountPercent.ShouldBe(10m);
        (await check.Set<ForecastRecommendation>().SingleAsync(r => r.Id == recommendationId)).AppliedPromotionId.ShouldBe(promotion.Id);
    }

    [Fact]
    public async Task Should_ListMoreItemsWithPendingRecommendation_When_DashboardAsksForMore()
    {
        var seeded = await Seed();
        using var scope = seeded.Services.CreateScope();
        var lots = Use<IGetExpiringLotsSummaryUseCase>(scope);

        // Janela larga: os lotes de compra do histórico vencem em até 180 dias.
        var all = (await lots.Execute(null, 365, limit: 200)).Data!;
        all.Count.ShouldBeGreaterThan(5);
        all.Items.Count.ShouldBe(Math.Min(all.Count, 200));
        all.Items.ShouldAllBe(i => i.LotId != Guid.Empty && i.LocationId != Guid.Empty && i.StoreId != Guid.Empty && i.StoreName != string.Empty && i.ProductId != Guid.Empty);

        // Padrão do painel continua 5; limite fora da faixa volta ao padrão.
        (await lots.Execute(null, 365)).Data!.Items.Count.ShouldBe(5);
        (await lots.Execute(null, 365, limit: 0)).Data!.Items.Count.ShouldBe(5);

        // O item traz a recomendação pendente do produto (menos "Manter"), quando existe.
        var pending = (await Pending(seeded, RecommendationActionEnum.Promote))
            .Concat(await Pending(seeded, RecommendationActionEnum.Buy))
            .Concat(await Pending(seeded, RecommendationActionEnum.Transfer))
            .ToDictionary(r => r.ProductId, r => r.Id);
        var withRecommendation = all.Items.Where(i => pending.ContainsKey(i.ProductId)).ToList();
        withRecommendation.ShouldNotBeEmpty();
        withRecommendation.ShouldAllBe(i => i.RecommendationId == pending[i.ProductId] && i.RecommendationAction != null);
        all.Items.Where(i => !pending.ContainsKey(i.ProductId)).ShouldAllBe(i => i.RecommendationId == null);
    }

    [Fact]
    [Trait("Rule", "F3.4")]
    public async Task Should_DismissWithReason_When_RecommendationIsPending()
    {
        var seeded = await Seed();
        var target = (await Pending(seeded, RecommendationActionEnum.Hold)).Last();

        using var scope = seeded.Services.CreateScope();
        var dismiss = Use<IDismissRecommendationUseCase>(scope);

        var missingNote = await dismiss.Execute(target.Id, new DismissRecommendationDto { ReasonCode = "Other" }, seeded.AdminId);
        missingNote.ErrorCode.ShouldBe(422);
        missingNote.FieldErrors!.Keys.ShouldContain("note");

        var result = await dismiss.Execute(target.Id, new DismissRecommendationDto { ReasonCode = "Other", Note = "Fornecedor parou de vender." }, seeded.AdminId);
        result.IsSuccess.ShouldBeTrue(result.Message);
        result.Data!.Status.ShouldBe("Dismissed");

        (await dismiss.Execute(target.Id, new DismissRecommendationDto(), seeded.AdminId)).ErrorCode.ShouldBe(400);

        await using var check = fixture.CreateContext(seeded.Connection);
        var saved = await check.Set<ForecastRecommendation>().SingleAsync(r => r.Id == target.Id);
        saved.DismissReasonCode.ShouldBe(DismissReasonEnum.Other);
        saved.DismissReason.ShouldBe("Fornecedor parou de vender.");
    }

    [Fact]
    [Trait("Rule", "RN-DF21")]
    public async Task Should_ReturnDetailWithSplitAndProductForecast_When_RecommendationIsBuy()
    {
        var seeded = await Seed();
        var buy = (await Pending(seeded, RecommendationActionEnum.Buy)).First(r => r.Items.Count > 1);

        using var scope = seeded.Services.CreateScope();
        var result = await Use<IGetForecastRecommendationUseCase>(scope).Execute(buy.Id);

        result.IsSuccess.ShouldBeTrue(result.Message);
        var detail = result.Data!;
        detail.ReasonCalculated.ShouldNotBeNullOrEmpty();
        detail.ReasonAi.ShouldNotBeNull(); // a IA fake sempre devolve "IA: ..."
        detail.Calculation.LeadTimeDays.ShouldBe(buy.LeadTimeDays);
        detail.VariantSplit.Count.ShouldBe(buy.Items.Count);
        detail.VariantSplit.Sum(v => v.SuggestedQuantity).ShouldBe(buy.SuggestedQuantity!.Value);
        detail.Product.History.Count.ShouldBe(24);
        detail.Product.Projection.ShouldNotBeEmpty();
        detail.Product.Seasonality.Count.ShouldBe(12);
        detail.Product.LatestRecommendationId.ShouldNotBeNull();

        (await Use<IGetForecastRecommendationUseCase>(scope).Execute(Guid.NewGuid())).ErrorCode.ShouldBe(404);
        (await Use<IGetProductForecastUseCase>(scope).Execute(buy.ProductId, 12)).Data!.History.Count.ShouldBe(12);
    }

    [Fact]
    [Trait("Rule", "T-DF2")]
    public async Task Should_AddSelectedStoreStock_When_StoreIdIsGiven()
    {
        var seeded = await Seed();
        Guid storeId;
        Dictionary<Guid, decimal> stockAtStore;
        Dictionary<Guid, decimal> incomingAtStore;
        await using (var context = fixture.CreateContext(seeded.Connection))
        {
            storeId = await context.Set<Store>().Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => s.Id).FirstAsync();
            stockAtStore = await context.Set<StockBalance>()
                .Where(b => b.Location.StoreId == storeId && b.Location.IsActive)
                .GroupBy(b => b.Variant.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(b => b.Quantity));
            incomingAtStore = await context.Set<PurchaseItem>()
                .Where(i => i.PurchaseOrder.StoreId == storeId
                    && (i.PurchaseOrder.Status == PurchaseStatusEnum.Budget || i.PurchaseOrder.Status == PurchaseStatusEnum.Pending))
                .GroupBy(i => i.Variant.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(i => i.Quantity));
        }

        using var scope = seeded.Services.CreateScope();
        var list = Use<IGetForecastRecommendationsUseCase>(scope);

        // Sem loja: só os números da análise (empresa inteira).
        var general = (await list.Execute(seeded.RunId, 0, 20, null)).Data!.Items;
        general.ShouldNotBeEmpty();
        general.ShouldAllBe(r => r.StoreCurrentStock == null && r.StoreIncomingStock == null);

        // Com loja: saldo e "a caminho" daquela loja; a sugestão continua a da análise.
        var byStore = (await list.Execute(seeded.RunId, 0, 20, null, storeId: storeId)).Data!.Items;
        byStore.Select(r => r.Id).ShouldBe(general.Select(r => r.Id));
        foreach (var row in byStore)
        {
            row.StoreCurrentStock.ShouldBe(stockAtStore.GetValueOrDefault(row.ProductId));
            row.StoreIncomingStock.ShouldBe(incomingAtStore.GetValueOrDefault(row.ProductId));
            row.SuggestedQuantity.ShouldBe(general.Single(g => g.Id == row.Id).SuggestedQuantity);
        }

        var top = (await Use<IGetForecastTopUseCase>(scope).Execute(5, storeId)).Data!;
        foreach (var row in top.Recommendations)
            row.StoreCurrentStock.ShouldBe(stockAtStore.GetValueOrDefault(row.ProductId));

        var detail = (await Use<IGetForecastRecommendationUseCase>(scope).Execute(byStore[0].Id, storeId)).Data!;
        detail.StoreCurrentStock.ShouldBe(stockAtStore.GetValueOrDefault(detail.ProductId));
    }

    // ---------------------------------------------------------------- F3.5: painel (T-DF1) e resumo diário (L-DF4)

    [Fact]
    [Trait("Rule", "T-DF1")]
    public async Task Should_FillDashboardBlocks_When_SeedHasHistory()
    {
        var seeded = await Seed();
        using var scope = seeded.Services.CreateScope();

        // KPIs: o mês corrente até 26/10 bate com as vendas pagas do banco.
        var kpis = (await Use<IGetSalesKpisUseCase>(scope).Execute(null)).Data!;
        await using (var context = fixture.CreateContext(seeded.Connection))
        {
            var clock = Use<IBusinessClock>(scope);
            var from = clock.StartOfDayUtc(new DateOnly(2026, 10, 1));
            var to = clock.StartOfDayUtc(Anchor.AddDays(1));
            var paid = await context.Set<Order>().Where(o => o.Status == OrderStatusEnum.Paid && o.PaidAt >= from && o.PaidAt < to).ToListAsync();
            kpis.Month.ShouldBe("2026-10");
            kpis.OrderCount.ShouldBe(paid.Count);
            kpis.Revenue.ShouldBe(paid.Sum(o => o.Total));
        }
        kpis.Revenue.ShouldBeGreaterThan(0);
        kpis.GrossMargin.ShouldBeLessThan(kpis.Revenue);
        kpis.RevenueVsLastMonthPercent.ShouldNotBeNull();

        var chart = (await Use<IGetSalesChartUseCase>(scope).Execute(null)).Data!;
        chart.Months.Count.ShouldBe(12);
        chart.Months[^1].Month.ShouldBe("2026-10");
        chart.Months.ShouldContain(m => m.RevenueLastYear != null); // 14 meses de seed: os 2 primeiros meses têm o ano anterior

        var lowStock = (await Use<IGetLowStockSummaryUseCase>(scope).Execute(null)).Data!;
        lowStock.Items.Count.ShouldBe(Math.Min(5, lowStock.Count));
        lowStock.Items.ShouldAllBe(i => i.Available < i.MinQuantity && i.StoreId != Guid.Empty && i.StoreName != string.Empty);

        var lots = (await Use<IGetExpiringLotsSummaryUseCase>(scope).Execute(null, 365)).Data!;
        lots.Items.Count.ShouldBe(Math.Min(5, lots.Count));
        lots.Items.ShouldAllBe(l => l.DaysToExpire >= 0 && l.DaysToExpire <= 365);

        var events = (await Use<IGetUpcomingEventsUseCase>(scope).Execute(90)).Data!;
        events.ShouldNotBeEmpty(); // Black Friday e Natal caem nos 90 dias depois de 26/10
        events.ShouldAllBe(e => e.NextDate >= Anchor && e.NextDate <= Anchor.AddDays(90) && e.DemandStartsOn <= e.NextDate);

        var top = (await Use<IGetForecastTopUseCase>(scope).Execute(5)).Data!;
        top.Run.ShouldNotBeNull();
        top.Recommendations.Count.ShouldBeLessThanOrEqualTo(5);
        top.Recommendations.ShouldAllBe(r => r.Status == "Pending" && r.Action != "Hold");
    }

    [Fact]
    [Trait("Rule", "L-DF4")]
    public async Task Should_PublishDailyDigestOnce_When_StockIsLow()
    {
        var seeded = await Seed();
        int lowStock;
        using (var scope = seeded.Services.CreateScope())
        {
            lowStock = (await Use<IGetLowStockSummaryUseCase>(scope).Execute(null)).Data!.Count;
            await Use<INotificationDigestService>(scope).RunAsync(CancellationToken.None);
        }
        using (var scope = seeded.Services.CreateScope())
            await Use<INotificationDigestService>(scope).RunAsync(CancellationToken.None); // de novo no mesmo dia: nada novo

        await using var context = fixture.CreateContext(seeded.Connection);
        var digest = await context.Set<Notification>().Where(n => n.Type == NotificationTypeEnum.LowStock && n.UserId == seeded.AdminId).ToListAsync();
        if (lowStock == 0)
            digest.ShouldBeEmpty();
        else
        {
            digest.Count.ShouldBe(1);
            digest[0].DigestDate.ShouldBe(Anchor);
            digest[0].Message.ShouldContain(lowStock.ToString());
        }
    }

    /// <summary>Todas as recomendações "Comprar" da análise, em qualquer status: os 422 da F3.4c não dependem de sobrar pendentes.</summary>
    private async Task<List<ForecastRecommendation>> AllBuys(Seeded seeded)
    {
        await using var context = fixture.CreateContext(seeded.Connection);
        return await context.Set<ForecastRecommendation>()
            .Include(r => r.Items)
            .Include(r => r.Product).ThenInclude(p => p.Suppliers)
            .Where(r => r.ForecastRunId == seeded.RunId && r.Action == RecommendationActionEnum.Buy && r.Items.Any(i => i.Quantity > 0))
            .OrderBy(r => r.Product.Name)
            .ToListAsync();
    }

    /// <summary>Quantas compras gravadas com estas variações existem (para provar que um 422 não grava nada).</summary>
    private async Task<int> OrdersWith(Seeded seeded, IEnumerable<Guid> variantIds)
    {
        var ids = variantIds.ToList();
        await using var context = fixture.CreateContext(seeded.Connection);
        return await context.Set<PurchaseOrder>().CountAsync(p => p.Items.Any(i => ids.Contains(i.VariantId)));
    }

    [Fact]
    [Trait("Rule", "F3.4c")]
    public async Task Should_PointToSecondGroupSupplier_When_ItIsNotActive()
    {
        var seeded = await Seed();
        var location = await ReceivingLocation(seeded);
        var buys = (await AllBuys(seeded)).Where(r => r.Product.Suppliers.Any(s => s.IsPreferred)).ToList();
        var first = buys[0];
        var second = buys[1];
        var groups = new List<ApplyPurchaseGroupDto>
        {
            Group(first.Product.Suppliers.First(s => s.IsPreferred).SupplierId, location, first),
            Group(Guid.NewGuid(), location, second), // fornecedor que não existe ativo
        };
        var variants = groups.SelectMany(g => g.Items).Select(i => i.VariantId).ToList();
        var before = await OrdersWith(seeded, variants);

        using var scope = seeded.Services.CreateScope();
        var result = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto { Groups = groups }, seeded.AdminId);

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.Keys.Where(k => k.EndsWith("supplierId", StringComparison.Ordinal)).ShouldBe(["groups[1].supplierId"]);
        (await OrdersWith(seeded, variants)).ShouldBe(before);
    }

    [Fact]
    [Trait("Rule", "F3.4c")]
    public async Task Should_PointToEveryRequestLine_When_SummedVariantIsInvalid()
    {
        var seeded = await Seed();
        var location = await ReceivingLocation(seeded);
        var whole = (await AllBuys(seeded))
            .Where(r => r.Product.DecimalPlaces == 0)
            .Take(2)
            .ToList();
        whole.Count.ShouldBe(2);
        await using var context = fixture.CreateContext(seeded.Connection);
        var supplierId = await context.Set<Supplier>().Where(s => s.IsActive).OrderBy(s => s.Name).Select(s => s.Id).FirstAsync();
        var valid = whole[0].Items.First(i => i.Quantity > 0).VariantId;
        var repeated = whole[1].Items.First(i => i.Quantity > 0).VariantId;

        // Linha 0 válida; linhas 1 e 2 com a mesma variação: na lista somada ela é o item 1 (0,5 + 0,7 = 1,2, quebrado num produto inteiro).
        var group = new ApplyPurchaseGroupDto
        {
            SupplierId = supplierId,
            ReceiveLocationId = location,
            Items =
            [
                new() { RecommendationId = whole[0].Id, VariantId = valid, Quantity = 1, UnitCost = 5 },
                new() { RecommendationId = whole[1].Id, VariantId = repeated, Quantity = 0.5m, UnitCost = 5 },
                new() { RecommendationId = whole[1].Id, VariantId = repeated, Quantity = 0.7m, UnitCost = 5 },
            ],
        };
        var before = await OrdersWith(seeded, [valid, repeated]);

        using var scope = seeded.Services.CreateScope();
        var result = await Use<IApplyPurchaseRecommendationsUseCase>(scope).Execute(new ApplyPurchaseDto { Groups = [group] }, seeded.AdminId);

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.Keys.Where(k => k.EndsWith("quantity", StringComparison.Ordinal)).Order().ShouldBe(["groups[0].items[1].quantity", "groups[0].items[2].quantity"]);
        (await OrdersWith(seeded, [valid, repeated])).ShouldBe(before);
    }
}
