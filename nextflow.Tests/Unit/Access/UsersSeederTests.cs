using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Seeders;
using NSubstitute;

namespace Nextflow.Tests.Unit.Access;

public class UsersSeederTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private UsersSeeder Seeder(IUserRepository repository, string environmentName, string? password)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:Admin:Password"] = password })
            .Build();

        return new UsersSeeder(repository, _unitOfWork, AccessTestData.Hasher(), configuration, environment);
    }

    [Theory]
    [InlineData("Staging", null)]
    [InlineData("Production", "")]
    [InlineData("Staging", "   ")]
    public async Task Should_FailNamingTheVariable_When_OutsideDevelopmentWithoutPassword(string environment, string? password)
    {
        var repository = AccessTestData.Repository();

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Seeder(repository, environment, password).SeedAsync(default));

        error.Message.ShouldContain("Seed__Admin__Password");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_NotRequirePassword_When_UsersAlreadyExistOutsideDevelopment()
    {
        await Seeder(AccessTestData.Repository(AccessTestData.NewUser()), "Staging", null).SeedAsync(default);

        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Staging", "Definida123", "hash:Definida123")]
    [InlineData("Development", "Definida123", "hash:Definida123")]
    [InlineData("Development", null, "hash:!Admin123")]
    public async Task Should_AddAdminAndCommitOnce_When_DatabaseHasNoUser(string environment, string? password, string expectedHash)
    {
        var repository = AccessTestData.Repository();
        User? saved = null;
        await repository.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>());

        await Seeder(repository, environment, password).SeedAsync(default);

        saved!.Email.ShouldBe(UsersSeeder.AdminEmail);
        saved.PasswordHash.ShouldBe(expectedHash);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
