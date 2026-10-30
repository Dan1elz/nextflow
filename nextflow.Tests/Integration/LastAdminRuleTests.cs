using Microsoft.EntityFrameworkCore;
using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Users;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Repositories;

namespace Nextflow.Tests.Integration;

/// <summary>
/// RN-AC07 contra o Postgres: o predicado do use case vira SQL de verdade e o resultado é lido de volta do banco. Banco
/// próprio, porque a regra olha todos os usuários e os outros testes deixam Admins ativos.
/// </summary>
public class LastAdminRuleTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string CpfA = "39053344705";
    private const string CpfB = "86288366757";
    private const string CpfC = "71460238001";

    public async Task InitializeAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Set<User>().ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> Seed(string email, string cpf, bool active = true)
    {
        var user = new User(new UserRequestDto
        {
            Name = "Admin",
            LastName = "Teste",
            Cpf = cpf,
            BirthDate = new DateOnly(1990, 5, 20),
            Email = email,
            Password = "Senha123",
        }, "hash");
        if (!active)
            user.Delete();

        await using var context = fixture.CreateContext();
        context.Set<User>().Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>Executa o use case real com o repositório e o commit reais, em um contexto só dele (como uma requisição).</summary>
    private async Task<Domain.Dtos.ResultDto<bool>> Inactivate(Guid target, Guid actor)
    {
        await using var context = fixture.CreateContext();
        var useCase = new DeleteUserUseCase(new UserRepository(context), new UnitOfWork(context), new ResultFactory<bool>());
        return await useCase.Execute(target, actor);
    }

    private async Task<bool> IsActiveInDatabase(Guid id)
    {
        await using var context = fixture.CreateContext();
        return await context.Set<User>().Where(u => u.Id == id).Select(u => u.IsActive).SingleAsync();
    }

    [Fact]
    [Trait("Rule", "RN-AC07")]
    [Trait("Rule", "L-AC2")]
    public async Task Should_Return400AndKeepActiveInDatabase_When_TargetIsTheLastActiveAdmin()
    {
        var last = await Seed("ultimo@nextflow.com", CpfA);
        // O ator é um Admin já inativo: a requisição dele passou pelo OnTokenValidated antes da inativação.
        var staleActor = await Seed("antigo@nextflow.com", CpfB, active: false);

        var result = await Inactivate(last.Id, staleActor.Id);

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldBe("Não é possível inativar o último administrador ativo.");
        (await IsActiveInDatabase(last.Id)).ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-AC07")]
    [Trait("Rule", "L-AC2")]
    public async Task Should_InactivateUntilOneRemainsThenRefuse_When_AdminsAreInactivatedInSequence()
    {
        var keeper = await Seed("fica@nextflow.com", CpfA);
        var second = await Seed("segundo@nextflow.com", CpfB);
        var third = await Seed("terceiro@nextflow.com", CpfC);

        (await Inactivate(second.Id, keeper.Id)).IsSuccess.ShouldBeTrue();
        (await Inactivate(third.Id, keeper.Id)).IsSuccess.ShouldBeTrue();
        var refused = await Inactivate(keeper.Id, third.Id);

        refused.ErrorCode.ShouldBe(400);
        (await IsActiveInDatabase(keeper.Id)).ShouldBeTrue();
        (await IsActiveInDatabase(second.Id)).ShouldBeFalse();
        (await IsActiveInDatabase(third.Id)).ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-AC07")]
    [Trait("Rule", "L-AC2")]
    public async Task Should_RefuseAsOwnUserFirst_When_LastActiveAdminInactivatesHimself()
    {
        var last = await Seed("sozinho@nextflow.com", CpfA);

        var result = await Inactivate(last.Id, last.Id);

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldBe("Você não pode inativar o seu próprio usuário.");
        (await IsActiveInDatabase(last.Id)).ShouldBeTrue();
    }
}
