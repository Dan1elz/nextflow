using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Models;
using Nextflow.Domain.ValueObjects;
using Nextflow.Infrastructure.Repositories;

namespace Nextflow.Tests.Integration;

[Collection(PostgresCollection.Name)]
public class UserPersistenceTests(PostgresFixture fixture)
{
    // CPFs válidos exclusivos desta classe: o banco é compartilhado na coleção.
    private const string CpfA = "39053344705";
    private const string CpfB = "86288366757";
    private const string CpfC = "71460238001";
    private const string CpfD = "24681357928";

    private static User NewUser(string email, string cpf) => new(new UserRequestDto
    {
        Name = "Maria",
        LastName = "Silva",
        Cpf = cpf,
        BirthDate = new DateOnly(1990, 5, 20),
        Email = email,
        Password = "Senha123",
    }, "hash");

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_TranslateCpfComparisonWithConverter_When_CheckingExistence()
    {
        await using (var context = fixture.CreateContext())
        {
            context.Set<User>().Add(NewUser("cpf@nextflow.com", CpfA));
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var repository = new UserRepository(readContext);

        var existing = TaxDocument.ParseCpf(CpfA);
        var other = TaxDocument.ParseCpf("00000000191");

        (await repository.Exists(u => u.Cpf == existing)).ShouldBeTrue();
        (await repository.Exists(u => u.Cpf == other)).ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_RejectDuplicate_When_EmailOrCpfAlreadyExistInTheDatabase()
    {
        await using (var context = fixture.CreateContext())
        {
            context.Set<User>().Add(NewUser("unico@nextflow.com", CpfB));
            await context.SaveChangesAsync();
        }

        await using (var sameEmail = fixture.CreateContext())
        {
            sameEmail.Set<User>().Add(NewUser("unico@nextflow.com", CpfC));
            await Should.ThrowAsync<DbUpdateException>(() => sameEmail.SaveChangesAsync());
        }

        await using var sameCpf = fixture.CreateContext();
        sameCpf.Set<User>().Add(NewUser("outro@nextflow.com", CpfB));
        await Should.ThrowAsync<DbUpdateException>(() => sameCpf.SaveChangesAsync());
    }

    [Fact]
    public async Task Should_ReadCpfBackAsValueObject_When_LoadingUser()
    {
        var user = NewUser("leitura@nextflow.com", CpfD);
        await using (var context = fixture.CreateContext())
        {
            context.Set<User>().Add(user);
            await context.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateContext();
        var loaded = await new UserRepository(readContext).GetById(user.Id);

        loaded!.Cpf.Value.ShouldBe(CpfD);
        loaded.FullName.ShouldBe("Maria Silva");
    }
}
