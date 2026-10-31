using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Inventory;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Stores;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Stores;
using NSubstitute;

namespace Nextflow.Tests.Unit.Inventory;

public class StoreRulesTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static Store NewStore(out StockLocation location, string name = "Centro")
    {
        var store = new Store(new StoreRequestDto { Name = name });
        location = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = "Loja", IsSellable = true });
        store.Locations.Add(location);
        store.SetDefaults(location.Id, location.Id);
        return store;
    }

    private static StockLocation NewLocation(Store store, string name = "Depósito", bool sellable = false)
    {
        var location = new StockLocation(new LocationRequestDto { StoreId = store.Id, Name = name, IsSellable = sellable });
        store.Locations.Add(location);
        return location;
    }

    private static IStoreRepository Stores(params Store[] stores)
    {
        var repository = Substitute.For<IStoreRepository>();
        repository.Exists(Arg.Any<Expression<Func<Store, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => stores.AsQueryable().Any(call.Arg<Expression<Func<Store, bool>>>()));
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Store>, IQueryable<Store>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => stores.FirstOrDefault(s => s.Id == call.Arg<Guid>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Store>, IQueryable<Store>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => stores.FirstOrDefault(s => s.Id == call.Arg<Guid>()));
        return repository;
    }

    private static IStockLocationRepository Locations(params StockLocation[] locations)
    {
        var repository = Substitute.For<IStockLocationRepository>();
        repository.Exists(Arg.Any<Expression<Func<StockLocation, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => locations.AsQueryable().Any(call.Arg<Expression<Func<StockLocation, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<StockLocation>, IQueryable<StockLocation>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => locations.FirstOrDefault(l => l.Id == call.Arg<Guid>()));
        return repository;
    }

    private DeleteLocationUseCase DeleteLocation(Store store)
        => new(Locations([.. store.Locations]), Stores(store), _unitOfWork, new ResultFactory<bool>());

    private UpdateStoreUseCase UpdateStore(Store store)
        => new(Stores(store), _unitOfWork, new UpdateStoreRequestDtoValidator(), new ResultFactory<StoreResponseDto>());

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_CreateSellableLocationAsBothDefaultsAndLinkAdminsInOneCommit_When_CreatingStore()
    {
        var repository = Substitute.For<IStoreRepository>();
        Store? saved = null;
        await repository.AddAsync(Arg.Do<Store>(s => saved = s), Arg.Any<CancellationToken>());
        var membership = Substitute.For<IStoreMembership>();
        var useCase = new CreateStoreUseCase(repository, membership, _unitOfWork, new StoreRequestDtoValidator(), new ResultFactory<StoreResponseDto>());

        var result = await useCase.Execute(new StoreRequestDto { Name = " Centro " });

        result.IsSuccess.ShouldBeTrue();
        saved!.Name.ShouldBe("Centro");
        var location = saved.Locations.ShouldHaveSingleItem();
        location.Name.ShouldBe("Loja");
        location.IsSellable.ShouldBeTrue();
        location.StoreId.ShouldBe(saved.Id);
        saved.DefaultSaleLocationId.ShouldBe(location.Id);
        saved.DefaultReceiveLocationId.ShouldBe(location.Id);
        result.Data!.Locations.ShouldHaveSingleItem().HasStock.ShouldBeFalse();
        await membership.Received(1).LinkStore(saved.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return422_When_DefaultSaleLocationIsNotSellable()
    {
        var store = NewStore(out var main);
        var deposit = NewLocation(store);

        var result = await UpdateStore(store).Execute(store.Id, new UpdateStoreRequestDto
        {
            Name = "Centro",
            DefaultSaleLocationId = deposit.Id,
            DefaultReceiveLocationId = main.Id,
        });

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["defaultSaleLocationId"].ShouldBe(["O local padrão de venda precisa ser vendável."]);
        store.DefaultSaleLocationId.ShouldBe(main.Id);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_AcceptNonSellableAsReceive_When_SavingDefaults()
    {
        var store = NewStore(out var main);
        var deposit = NewLocation(store);

        var result = await UpdateStore(store).Execute(store.Id, new UpdateStoreRequestDto
        {
            Name = "Centro",
            DefaultSaleLocationId = main.Id,
            DefaultReceiveLocationId = deposit.Id,
        });

        result.IsSuccess.ShouldBeTrue();
        store.DefaultReceiveLocationId.ShouldBe(deposit.Id);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return422_When_DefaultIsInactiveOrFromAnotherStore()
    {
        var store = NewStore(out var main);
        var inactive = NewLocation(store, sellable: true);
        inactive.Delete();
        var other = NewStore(out var foreign, "Outra");

        var inactiveResult = await UpdateStore(store).Execute(store.Id, new UpdateStoreRequestDto { Name = "Centro", DefaultSaleLocationId = inactive.Id, DefaultReceiveLocationId = main.Id });
        var foreignResult = await UpdateStore(store).Execute(store.Id, new UpdateStoreRequestDto { Name = "Centro", DefaultSaleLocationId = main.Id, DefaultReceiveLocationId = foreign.Id });

        inactiveResult.FieldErrors!.ShouldContainKey("defaultSaleLocationId");
        foreignResult.FieldErrors!.ShouldContainKey("defaultReceiveLocationId");
        other.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return400_When_InactivatingDefaultLocation()
    {
        var store = NewStore(out var main);
        NewLocation(store, sellable: true);

        var result = await DeleteLocation(store).Execute(main.Id);

        result.ErrorCode.ShouldBe(400);
        main.IsActive.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return400_When_InactivatingLocationThatIsOnlyReceiveDefault()
    {
        var store = NewStore(out var main);
        var deposit = NewLocation(store);
        store.SetDefaults(main.Id, deposit.Id);

        var result = await DeleteLocation(store).Execute(deposit.Id);

        result.ErrorCode.ShouldBe(400);
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Inactivate_When_LocationIsNotDefault()
    {
        var store = NewStore(out _);
        var deposit = NewLocation(store);

        var result = await DeleteLocation(store).Execute(deposit.Id);

        result.IsSuccess.ShouldBeTrue();
        deposit.IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CE35")]
    public async Task Should_Return422OnIsSellable_When_UnmarkingDefaultSaleLocation()
    {
        var store = NewStore(out var main);
        var useCase = new UpdateLocationUseCase(Locations(main), Stores(store), _unitOfWork, new UpdateLocationRequestDtoValidator(), new ResultFactory<LocationResponseDto>());

        var result = await useCase.Execute(main.Id, new UpdateLocationRequestDto { Name = "Loja", IsSellable = false });

        result.FieldErrors!.ShouldContainKey("isSellable");
        main.IsSellable.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_Return400_When_InactivatingLastActiveStore()
    {
        var last = NewStore(out _);
        var inactive = NewStore(out _, "Antiga");
        inactive.Delete();

        var result = await new DeleteStoreUseCase(Stores(last, inactive), _unitOfWork, new ResultFactory<bool>()).Execute(last.Id);

        result.ErrorCode.ShouldBe(400);
        last.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_KeepLocationsAsTheyAre_When_InactivatingStore()
    {
        var store = NewStore(out var main);
        var other = NewStore(out _, "Outra");

        var result = await new DeleteStoreUseCase(Stores(store, other), _unitOfWork, new ResultFactory<bool>()).Execute(store.Id);

        result.IsSuccess.ShouldBeTrue();
        store.IsActive.ShouldBeFalse();
        main.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_LinkMissingAdmins_When_ReactivatingStore()
    {
        var store = NewStore(out _);
        store.Delete();
        var membership = Substitute.For<IStoreMembership>();

        var result = await new ReactivateStoreUseCase(Stores(store), membership, _unitOfWork, new ResultFactory<bool>()).Execute(store.Id);

        result.IsSuccess.ShouldBeTrue();
        await membership.Received(1).LinkStore(store.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CE36")]
    public async Task Should_Return400_When_ChangingLocationsOfInactiveStore()
    {
        var store = NewStore(out var main);
        store.Delete();
        var create = new CreateLocationUseCase(Locations(), Stores(store), _unitOfWork, new LocationRequestDtoValidator(), new ResultFactory<LocationResponseDto>());
        var update = new UpdateLocationUseCase(Locations(main), Stores(store), _unitOfWork, new UpdateLocationRequestDtoValidator(), new ResultFactory<LocationResponseDto>());

        (await create.Execute(new LocationRequestDto { StoreId = store.Id, Name = "Depósito" })).ErrorCode.ShouldBe(400);
        (await update.Execute(main.Id, new UpdateLocationRequestDto { Name = "Novo" })).ErrorCode.ShouldBe(400);
        main.Name.ShouldBe("Loja");
    }

    [Fact]
    public async Task Should_Return404_When_CreatingLocationForUnknownStore()
    {
        var create = new CreateLocationUseCase(Locations(), Stores(), _unitOfWork, new LocationRequestDtoValidator(), new ResultFactory<LocationResponseDto>());

        (await create.Execute(new LocationRequestDto { StoreId = Guid.NewGuid(), Name = "Depósito" })).ErrorCode.ShouldBe(404);
    }

    [Fact]
    [Trait("Rule", "RN-CE37")]
    public async Task Should_LinkOnlyMissingPairs_When_LinkingStoreAndUser()
    {
        var (linkedUser, newUser, linkedStore, newStore) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var userStores = Substitute.For<IUserStoreRepository>();
        userStores.GetUserIds(newStore, Arg.Any<CancellationToken>()).Returns([linkedUser]);
        userStores.GetStoreIds(newUser, Arg.Any<CancellationToken>()).Returns([linkedStore]);
        var users = Substitute.For<IUserRepository>();
        users.GetActiveAdminIds(Arg.Any<CancellationToken>()).Returns([linkedUser, newUser]);
        var stores = Substitute.For<IStoreRepository>();
        stores.GetActiveIds(Arg.Any<CancellationToken>()).Returns([linkedStore, newStore]);
        var added = new List<UserStore>();
        await userStores.AddRangeAsync(Arg.Do<IEnumerable<UserStore>>(items => added.AddRange(items)), Arg.Any<CancellationToken>());
        var membership = new StoreMembership(userStores, users, stores);

        await membership.LinkStore(newStore);
        await membership.LinkUser(newUser);

        added.Count.ShouldBe(2);
        added.ShouldAllBe(link => link.UserId == newUser && link.StoreId == newStore);
    }
}
