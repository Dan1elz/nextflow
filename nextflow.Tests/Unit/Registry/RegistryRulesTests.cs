using System.Linq.Expressions;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Registry;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Cities;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Addresses;
using Nextflow.Domain.Validators.Clients;
using Nextflow.Domain.Validators.Contacts;
using Nextflow.Domain.Validators.Suppliers;
using NSubstitute;

namespace Nextflow.Tests.Unit.Registry;

public class RegistryRulesTests
{
    private const string ValidCpf = "52998224725";
    private const string ValidCnpj = "11222333000181";
    private static readonly DateOnly Today = new(2026, 10, 30);

    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private static IBusinessClock Clock()
    {
        var clock = Substitute.For<IBusinessClock>();
        clock.Today.Returns(Today);
        return clock;
    }

    private static IClientRepository Clients(params Client[] clients)
    {
        var repository = Substitute.For<IClientRepository>();
        repository.Exists(Arg.Any<Expression<Func<Client, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => clients.AsQueryable().Any(call.Arg<Expression<Func<Client, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Client>, IQueryable<Client>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => clients.FirstOrDefault(c => c.Id == call.Arg<Guid>()));
        return repository;
    }

    private static ISupplierRepository Suppliers(params Supplier[] suppliers)
    {
        var repository = Substitute.For<ISupplierRepository>();
        repository.Exists(Arg.Any<Expression<Func<Supplier, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => suppliers.AsQueryable().Any(call.Arg<Expression<Func<Supplier, bool>>>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<Supplier>, IQueryable<Supplier>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => suppliers.FirstOrDefault(s => s.Id == call.Arg<Guid>()));
        return repository;
    }

    private CreateClientUseCase CreateClient(params Client[] clients)
        => new(Clients(clients), _unitOfWork, new ClientRequestDtoValidator(Clock()), new ResultFactory<ClientResponseDto>());

    private CreateSupplierUseCase CreateSupplier(params Supplier[] suppliers)
        => new(Suppliers(suppliers), _unitOfWork, new SupplierRequestDtoValidator(), new ResultFactory<SupplierResponseDto>());

    private static SupplierRequestDto SupplierDto(string cnpj = ValidCnpj, int leadTimeDays = 7)
        => new() { Name = "Doces Ltda", Cnpj = cnpj, LeadTimeDays = leadTimeDays };

    [Fact]
    [Trait("Rule", "RN-CD01")]
    public async Task Should_Return422OnCpf_When_CreatingClientWithRepeatedCpfInAnyFormat()
    {
        var existing = new Client(new ClientRequestDto { Name = "Ana", Cpf = ValidCpf });

        var result = await CreateClient(existing).Execute(new ClientRequestDto { Name = "Bia", Cpf = "529.982.247-25" });

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["cpf"].ShouldBe(["Já existe um cliente com este CPF."]);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD01")]
    public async Task Should_Return422OnCpf_When_CheckDigitsAreWrong()
    {
        var result = await CreateClient().Execute(new ClientRequestDto { Name = "Ana", Cpf = "52998224700" });

        result.FieldErrors!.ShouldContainKey("cpf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Rule", "L-CP6")]
    [Trait("Rule", "RN-CD01")]
    public async Task Should_CreateWithoutCpf_When_CpfIsNotInformed(string? cpf)
    {
        var existing = new Client(new ClientRequestDto { Name = "Sem Cpf" });

        var result = await CreateClient(existing).Execute(new ClientRequestDto { Name = "Ana", Cpf = cpf });

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Cpf.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CD01")]
    public async Task Should_Update_When_KeepingOwnCpf()
    {
        var client = new Client(new ClientRequestDto { Name = "Ana", Cpf = ValidCpf });
        var useCase = new UpdateClientUseCase(Clients(client), _unitOfWork, new ClientRequestDtoValidator(Clock()), new ResultFactory<ClientResponseDto>());

        var result = await useCase.Execute(client.Id, new ClientRequestDto { Name = "Ana Maria", Cpf = ValidCpf });

        result.IsSuccess.ShouldBeTrue();
        client.Name.ShouldBe("Ana Maria");
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void Should_RequirePastBirthDate_When_Informed(int daysFromToday, bool valid)
    {
        var dto = new ClientRequestDto { Name = "Ana", BirthDate = Today.AddDays(daysFromToday) };

        new ClientRequestDtoValidator(Clock()).Validate(dto).IsValid.ShouldBe(valid);
    }

    [Fact]
    [Trait("Rule", "RN-CD02")]
    public async Task Should_Return422OnCnpj_When_CreatingSupplierWithRepeatedCnpjInAnyFormat()
    {
        var existing = new Supplier(SupplierDto());

        var result = await CreateSupplier(existing).Execute(SupplierDto("11.222.333/0001-81"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["cnpj"].ShouldBe(["Já existe um fornecedor com este CNPJ."]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("11222333000100")]
    [InlineData(ValidCpf)]
    [Trait("Rule", "RN-CD02")]
    public void Should_RejectCnpj_When_MissingOrInvalid(string cnpj)
        => new SupplierRequestDtoValidator().Validate(SupplierDto(cnpj)).Errors
            .ShouldContain(e => e.PropertyName == nameof(SupplierRequestDto.Cnpj));

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(180, true)]
    [InlineData(181, false)]
    [Trait("Rule", "RN-CD03")]
    public void Should_LimitLeadTimeFrom1To180_When_ValidatingSupplier(int days, bool valid)
        => new SupplierRequestDtoValidator().Validate(SupplierDto(leadTimeDays: days)).IsValid.ShouldBe(valid);

    [Fact]
    [Trait("Rule", "RN-CD03")]
    public void Should_DefaultLeadTimeTo7_When_NotInformed()
        => new SupplierRequestDto().LeadTimeDays.ShouldBe(7);

    [Fact]
    [Trait("Rule", "RN-CD04")]
    public async Task Should_InactivateWithoutRemoving_When_DeletingClient()
    {
        var client = new Client(new ClientRequestDto { Name = "Ana" });
        var repository = Clients(client);

        var result = await new DeleteClientUseCase(repository, _unitOfWork, new ResultFactory<bool>()).Execute(client.Id);

        result.IsSuccess.ShouldBeTrue();
        client.IsActive.ShouldBeFalse();
        repository.DidNotReceive().Remove(Arg.Any<Client>());
    }

    [Fact]
    [Trait("Rule", "RN-CD04")]
    public async Task Should_Reactivate_When_SupplierIsInactive()
    {
        var supplier = new Supplier(SupplierDto());
        supplier.Delete();

        var result = await new ReactivateSupplierUseCase(Suppliers(supplier), _unitOfWork, new ResultFactory<bool>()).Execute(supplier.Id);

        result.IsSuccess.ShouldBeTrue();
        supplier.IsActive.ShouldBeTrue();
    }

    private static AddressRequestDto AddressDto(bool isMain = false, Guid? ownerId = null) => new()
    {
        OwnerType = OwnerTypeEnum.Supplier,
        OwnerId = ownerId ?? Guid.NewGuid(),
        ZipCode = "13010-111",
        Street = "Rua A",
        Number = "1",
        Neighborhood = "Centro",
        CityId = Guid.NewGuid(),
        IsMain = isMain,
    };

    private static IOwnerDirectory Owners(bool exists = true, bool active = true)
    {
        var owners = Substitute.For<IOwnerDirectory>();
        owners.Exists(Arg.Any<OwnerTypeEnum>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(exists);
        owners.IsActive(Arg.Any<OwnerTypeEnum>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(exists && active);
        return owners;
    }

    private CreateContactUseCase CreateContact(IContactRepository repository, bool ownerExists, bool ownerActive = true)
        => new(repository, Owners(ownerExists, ownerActive), _unitOfWork, new ContactRequestDtoValidator(), new ResultFactory<ContactResponseDto>());

    private UpdateContactUseCase UpdateContact(Contact contact, bool ownerActive = true, params Contact[] siblings)
    {
        var repository = ContactsWith(siblings);
        repository.GetByIdForUpdate(contact.Id, Arg.Any<Func<IQueryable<Contact>, IQueryable<Contact>>?>(), Arg.Any<CancellationToken>()).Returns(contact);
        return new UpdateContactUseCase(repository, Owners(active: ownerActive), _unitOfWork, new UpdateContactRequestDtoValidator(), new ResultFactory<ContactResponseDto>());
    }

    private static ICityRepository CityExists()
    {
        var cities = Substitute.For<ICityRepository>();
        cities.GetSummary(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new CitySummaryDto("Campinas", "SP"));
        return cities;
    }

    private static UpdateAddressRequestDto UpdateAddressDto(bool isMain = false) => new()
    {
        ZipCode = "13010111",
        Street = "Rua B",
        Number = "2",
        Neighborhood = "Centro",
        CityId = Guid.NewGuid(),
        IsMain = isMain,
    };

    private UpdateAddressUseCase UpdateAddress(Address address, bool ownerActive = true, params Address[] siblings)
    {
        var repository = Substitute.For<IAddressRepository>();
        repository.GetByIdForUpdate(address.Id, Arg.Any<Func<IQueryable<Address>, IQueryable<Address>>?>(), Arg.Any<CancellationToken>()).Returns(address);
        repository.GetAllForUpdate(Arg.Any<Expression<Func<Address, bool>>>(), Arg.Any<CancellationToken>()).Returns([.. siblings]);
        return new UpdateAddressUseCase(repository, CityExists(), Owners(active: ownerActive), _unitOfWork, new UpdateAddressRequestDtoValidator(), new ResultFactory<AddressResponseDto>());
    }

    [Fact]
    [Trait("Rule", "RN-CD10")]
    public async Task Should_Return400AndNotCommit_When_CreatingContactForInactiveOwner()
    {
        var result = await CreateContact(ContactsWith(), ownerExists: true, ownerActive: false).Execute(ContactDto());

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldContain("inativo");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD10")]
    public async Task Should_Return400AndKeepValue_When_EditingContactOfInactiveOwner()
    {
        var contact = new Contact(ContactDto());

        var result = await UpdateContact(contact, ownerActive: false).Execute(contact.Id, new UpdateContactRequestDto { Type = ContactTypeEnum.Phone, Value = "1133334444" });

        result.ErrorCode.ShouldBe(400);
        contact.Value.ShouldBe("11988887766");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD10")]
    public async Task Should_Return400AndNotCommit_When_CreatingAddressForInactiveOwner()
    {
        var useCase = new CreateAddressUseCase(
            Substitute.For<IAddressRepository>(), CityExists(), Owners(active: false), _unitOfWork, new AddressRequestDtoValidator(), new ResultFactory<AddressResponseDto>());

        var result = await useCase.Execute(AddressDto());

        result.ErrorCode.ShouldBe(400);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD10")]
    public async Task Should_Return400AndKeepStreet_When_EditingAddressOfInactiveOwner()
    {
        var address = new Address(AddressDto());

        var result = await UpdateAddress(address, ownerActive: false).Execute(address.Id, UpdateAddressDto());

        result.ErrorCode.ShouldBe(400);
        address.Street.ShouldBe("Rua A");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_LeaveWithoutMain_When_UnmarkingTheOnlyContact()
    {
        var contact = new Contact(ContactDto(isMain: true));

        var result = await UpdateContact(contact).Execute(contact.Id, new UpdateContactRequestDto { Type = ContactTypeEnum.Phone, Value = "11988887766" });

        result.Data!.IsMain.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_NotPromoteSibling_When_UnmarkingTheMainAddress()
    {
        var ownerId = Guid.NewGuid();
        var address = new Address(AddressDto(isMain: true, ownerId));
        var sibling = new Address(AddressDto(ownerId: ownerId));

        var result = await UpdateAddress(address, siblings: sibling).Execute(address.Id, UpdateAddressDto());

        result.Data!.IsMain.ShouldBeFalse();
        sibling.IsMain.ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_UnmarkSibling_When_EditingAddressAsMain()
    {
        var ownerId = Guid.NewGuid();
        var address = new Address(AddressDto(ownerId: ownerId));
        var sibling = new Address(AddressDto(isMain: true, ownerId));

        var result = await UpdateAddress(address, siblings: sibling).Execute(address.Id, UpdateAddressDto(isMain: true));

        result.Data!.IsMain.ShouldBeTrue();
        sibling.IsMain.ShouldBeFalse();
    }

    private static IContactRepository ContactsWith(params Contact[] siblings)
    {
        var repository = Substitute.For<IContactRepository>();
        repository.GetAllForUpdate(Arg.Any<Expression<Func<Contact, bool>>>(), Arg.Any<CancellationToken>()).Returns([.. siblings]);
        return repository;
    }

    private static ContactRequestDto ContactDto(bool isMain = false, Guid? ownerId = null) => new()
    {
        OwnerType = OwnerTypeEnum.Client,
        OwnerId = ownerId ?? Guid.NewGuid(),
        Type = ContactTypeEnum.Phone,
        Value = "(11) 98888-7766",
        IsMain = isMain,
    };

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_Return404AndNotCommit_When_OwnerDoesNotExist()
    {
        var result = await CreateContact(ContactsWith(), ownerExists: false).Execute(ContactDto());

        result.ErrorCode.ShouldBe(404);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_BecomeMain_When_ItIsTheFirstOfTheOwner()
    {
        var result = await CreateContact(ContactsWith(), ownerExists: true).Execute(ContactDto());

        result.Data!.IsMain.ShouldBeTrue();
        result.Data.Value.ShouldBe("11988887766");
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_UnmarkOthersInSameCommit_When_MarkingAsMain()
    {
        var ownerId = Guid.NewGuid();
        var current = new Contact(ContactDto(isMain: true, ownerId));

        var result = await CreateContact(ContactsWith(current), ownerExists: true).Execute(ContactDto(isMain: true, ownerId));

        result.Data!.IsMain.ShouldBeTrue();
        current.IsMain.ShouldBeFalse();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_KeepCurrentMain_When_NewOneIsNotMain()
    {
        var ownerId = Guid.NewGuid();
        var current = new Contact(ContactDto(isMain: true, ownerId));

        var result = await CreateContact(ContactsWith(current), ownerExists: true).Execute(ContactDto(ownerId: ownerId));

        result.Data!.IsMain.ShouldBeFalse();
        current.IsMain.ShouldBeTrue();
    }

    [Theory]
    [InlineData((OwnerTypeEnum)0)]
    [InlineData((OwnerTypeEnum)9)]
    [Trait("Rule", "RN-CD05")]
    public void Should_RejectOwnerType_When_OutOfEnum(OwnerTypeEnum ownerType)
    {
        var dto = AddressDto();
        dto.OwnerType = ownerType;

        new AddressRequestDtoValidator().Validate(dto).Errors.ShouldContain(e => e.PropertyName == nameof(AddressRequestDto.OwnerType));
    }

    [Theory]
    [InlineData("13010-111", true)]
    [InlineData("1301011", false)]
    [InlineData("", false)]
    public void Should_RequireEightDigits_When_ValidatingZipCode(string zipCode, bool valid)
    {
        var dto = AddressDto();
        dto.ZipCode = zipCode;

        new AddressRequestDtoValidator().Validate(dto).IsValid.ShouldBe(valid);
    }

    [Theory]
    [InlineData(ContactTypeEnum.Phone, "(11) 3333-4444", true)]
    [InlineData(ContactTypeEnum.WhatsApp, "11988887766", true)]
    [InlineData(ContactTypeEnum.Phone, "123", false)]
    [InlineData(ContactTypeEnum.Email, "a@b.com", true)]
    [InlineData(ContactTypeEnum.Email, "nao-e-email", false)]
    [InlineData(ContactTypeEnum.Email, "", false)]
    public void Should_ValidateValueByType_When_SavingContact(ContactTypeEnum type, string value, bool valid)
    {
        var dto = new UpdateContactRequestDto { Type = type, Value = value };

        new UpdateContactRequestDtoValidator().Validate(dto).IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Should_NormalizeEmailToLowerCase_When_CreatingContact()
    {
        var contact = new Contact(new ContactRequestDto
        {
            OwnerType = OwnerTypeEnum.Client,
            OwnerId = Guid.NewGuid(),
            Type = ContactTypeEnum.Email,
            Value = " Ana@Doces.COM ",
        });

        contact.Value.ShouldBe("ana@doces.com");
        contact.IsPhone.ShouldBeFalse();
    }

    [Fact]
    public async Task Should_BuildSummaryFromSeparateQueries_When_OwnerHasAddressAndContacts()
    {
        var ownerId = Guid.NewGuid();
        var addresses = Substitute.For<IAddressRepository>();
        addresses.GetMainCityStates(OwnerTypeEnum.Client, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string> { [ownerId] = "Campinas/SP" });
        var email = new Contact(new ContactRequestDto { OwnerType = OwnerTypeEnum.Client, OwnerId = ownerId, Type = ContactTypeEnum.Email, Value = "a@b.com", IsMain = true });
        var phone = new Contact(new ContactRequestDto { OwnerType = OwnerTypeEnum.Client, OwnerId = ownerId, Type = ContactTypeEnum.WhatsApp, Value = "11988887766" });
        var contacts = Substitute.For<IContactRepository>();
        contacts.GetByOwners(OwnerTypeEnum.Client, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([email, phone]);
        var directory = new OwnerDirectory(Substitute.For<IClientRepository>(), Substitute.For<ISupplierRepository>(), addresses, Substitute.For<ICityRepository>(), contacts);

        var summaries = await directory.GetSummaries(OwnerTypeEnum.Client, [ownerId]);

        summaries[ownerId].ShouldBe(new OwnerSummaryDto("Campinas/SP", "a@b.com", null));
    }

    private static OwnerDirectory DirectoryWith(Guid ownerId, params Contact[] contacts)
    {
        var repository = Substitute.For<IContactRepository>();
        repository.GetByOwners(OwnerTypeEnum.Client, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([.. contacts]);
        var addresses = Substitute.For<IAddressRepository>();
        addresses.GetMainCityStates(OwnerTypeEnum.Client, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, string>());
        return new OwnerDirectory(Substitute.For<IClientRepository>(), Substitute.For<ISupplierRepository>(), addresses, Substitute.For<ICityRepository>(), repository);
    }

    private static Contact OwnedContact(Guid ownerId, ContactTypeEnum type, string value, bool isMain)
        => new(new ContactRequestDto { OwnerType = OwnerTypeEnum.Client, OwnerId = ownerId, Type = type, Value = value, IsMain = isMain });

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_LeaveMainPhoneNull_When_ThereIsNoMainContact()
    {
        var ownerId = Guid.NewGuid();
        var directory = DirectoryWith(ownerId, OwnedContact(ownerId, ContactTypeEnum.Phone, "11988887766", isMain: false));

        var summaries = await directory.GetSummaries(OwnerTypeEnum.Client, [ownerId]);

        summaries[ownerId].MainPhone.ShouldBeNull();
        summaries[ownerId].MainContact.ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_LeaveMainPhoneNull_When_MainContactIsEmail()
    {
        var ownerId = Guid.NewGuid();
        var directory = DirectoryWith(ownerId,
            OwnedContact(ownerId, ContactTypeEnum.Email, "a@b.com", isMain: true),
            OwnedContact(ownerId, ContactTypeEnum.WhatsApp, "11988887766", isMain: false));

        var summaries = await directory.GetSummaries(OwnerTypeEnum.Client, [ownerId]);

        summaries[ownerId].MainPhone.ShouldBeNull();
        summaries[ownerId].MainContact.ShouldBe("a@b.com");
    }

    [Theory]
    [InlineData(ContactTypeEnum.Phone)]
    [InlineData(ContactTypeEnum.WhatsApp)]
    [Trait("Rule", "RN-CD06")]
    public async Task Should_ReturnMainPhone_When_MainContactIsPhoneType(ContactTypeEnum type)
    {
        var ownerId = Guid.NewGuid();
        var directory = DirectoryWith(ownerId, OwnedContact(ownerId, type, "11988887766", isMain: true));

        (await directory.GetSummaries(OwnerTypeEnum.Client, [ownerId]))[ownerId].MainPhone.ShouldBe("11988887766");
    }

    [Fact]
    public async Task Should_NotQuery_When_ThereAreNoOwners()
    {
        var addresses = Substitute.For<IAddressRepository>();
        var directory = new OwnerDirectory(Substitute.For<IClientRepository>(), Substitute.For<ISupplierRepository>(), addresses, Substitute.For<ICityRepository>(), Substitute.For<IContactRepository>());

        (await directory.GetSummaries(OwnerTypeEnum.Client, [])).ShouldBeEmpty();
        await addresses.DidNotReceive().GetMainCityStates(Arg.Any<OwnerTypeEnum>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_SendTwentyAtMostAndOnlyActive_When_ListingOptions()
    {
        var active = new Supplier(SupplierDto());
        var inactive = new Supplier(SupplierDto("11444777000161"));
        inactive.Delete();
        var repository = Substitute.For<ISupplierRepository>();
        Expression<Func<Supplier, bool>>? predicate = null;
        var limit = 0;
        repository.GetAll(Arg.Do<Expression<Func<Supplier, bool>>>(p => predicate = p), Arg.Any<int>(), Arg.Do<int>(l => limit = l),
                Arg.Any<Func<IQueryable<Supplier>, IQueryable<Supplier>>?>(), Arg.Any<Func<IQueryable<Supplier>, IOrderedQueryable<Supplier>>?>(), Arg.Any<CancellationToken>())
            .Returns([active]);

        var result = await new GetSupplierOptionsUseCase(repository, new ResultFactory<List<Option>>()).Execute(null);

        limit.ShouldBe(20);
        predicate!.Compile()(active).ShouldBeTrue();
        predicate.Compile()(inactive).ShouldBeFalse();
        result.Data![0].ShouldSatisfyAllConditions(
            o => o.Value.ShouldBe(active.Id.ToString()),
            o => o.Label.ShouldBe("Doces Ltda · 11.222.333/0001-81"));
    }
}
