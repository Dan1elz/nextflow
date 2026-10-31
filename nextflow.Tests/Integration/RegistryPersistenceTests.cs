using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Addresses;
using Nextflow.Domain.Dtos.Clients;
using Nextflow.Domain.Dtos.Contacts;
using Nextflow.Domain.Dtos.Suppliers;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Repositories;
using Nextflow.Infrastructure.Seeders;
using Npgsql;

namespace Nextflow.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class RegistryPersistenceTests(PostgresFixture fixture)
{
    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_RejectUnknownOwnerType_When_DatabaseReceivesIt()
    {
        var contact = new Contact(new ContactRequestDto
        {
            OwnerType = (OwnerTypeEnum)9,
            OwnerId = Guid.NewGuid(),
            Type = ContactTypeEnum.Phone,
            Value = "11988887766",
        });
        await using var context = fixture.CreateContext();
        context.Set<Contact>().Add(contact);

        var error = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        error.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("ck_contacts_owner_type");
    }

    [Fact]
    [Trait("Rule", "RN-CD05")]
    public async Task Should_RejectAddressWithoutCity_When_DatabaseReceivesIt()
    {
        var address = new Address(new AddressRequestDto
        {
            OwnerType = OwnerTypeEnum.Client,
            OwnerId = Guid.NewGuid(),
            ZipCode = "13010111",
            Street = "Rua A",
            Number = "1",
            Neighborhood = "Centro",
            CityId = Guid.NewGuid(),
        });
        await using var context = fixture.CreateContext();
        context.Set<Address>().Add(address);

        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(181)]
    [Trait("Rule", "RN-CD03")]
    public async Task Should_RejectLeadTimeOutOfRange_When_DatabaseReceivesIt(int leadTimeDays)
    {
        await using var context = fixture.CreateContext();
        context.Set<Supplier>().Add(new Supplier(new SupplierRequestDto { Name = "Prazo", Cnpj = "04252011000110", LeadTimeDays = leadTimeDays }));

        var error = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync());

        error.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("ck_suppliers_lead_time_days");
    }

    [Fact]
    [Trait("Rule", "RN-CD02")]
    public async Task Should_RejectRepeatedCnpj_When_DatabaseReceivesIt()
    {
        await using (var context = fixture.CreateContext())
        {
            context.Set<Supplier>().Add(new Supplier(new SupplierRequestDto { Name = "A", Cnpj = "11444777000161" }));
            await context.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateContext();
        duplicate.Set<Supplier>().Add(new Supplier(new SupplierRequestDto { Name = "B", Cnpj = "11444777000161" }));

        await Should.ThrowAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    [Trait("Rule", "RN-CD08")]
    public async Task Should_SeedOnceWithAllMunicipalities_When_RunningLocationsSeederTwice()
    {
        for (var run = 0; run < 2; run++)
        {
            await using var context = fixture.CreateContext();
            await new LocationsSeeder(new CountryRepository(context), new UnitOfWork(context)).SeedAsync(CancellationToken.None);
        }

        await using var read = fixture.CreateContext();
        (await read.Set<State>().CountAsync()).ShouldBe(27);
        (await read.Set<City>().CountAsync()).ShouldBe(5570);
        (await read.Set<Country>().CountAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    [Trait("Rule", "RN-CD01")]
    [Trait("Rule", "L-CP6")]
    public async Task Should_AcceptManyNullCpfsAndRejectRepeatedOne_When_SavingClients()
    {
        await using (var context = fixture.CreateContext())
        {
            context.Set<Client>().AddRange(new Client(new ClientRequestDto { Name = "A" }), new Client(new ClientRequestDto { Name = "B" }));
            context.Set<Client>().Add(new Client(new ClientRequestDto { Name = "C", Cpf = "93541134780" }));
            await context.SaveChangesAsync();
        }

        await using var duplicate = fixture.CreateContext();
        duplicate.Set<Client>().Add(new Client(new ClientRequestDto { Name = "D", Cpf = "93541134780" }));

        await Should.ThrowAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }
}
