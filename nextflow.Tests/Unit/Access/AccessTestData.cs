using System.Linq.Expressions;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using NSubstitute;

namespace Nextflow.Tests.Unit.Access;

/// <summary>Usuários e repositório em memória: as regras rodam com o predicado real do use case, sem banco.</summary>
internal static class AccessTestData
{
    public const string ValidCpf = "52998224725";
    public const string OtherValidCpf = "11144477735";
    public const string Password = "Senha123";

    public static UserRequestDto Request(string email = "maria@nextflow.com", string cpf = ValidCpf, string password = Password) => new()
    {
        Name = "Maria",
        LastName = "Silva",
        Cpf = cpf,
        BirthDate = new DateOnly(1990, 5, 20),
        Email = email,
        Password = password,
    };

    public static User NewUser(string email = "maria@nextflow.com", string cpf = ValidCpf, string passwordHash = "hash")
        => new(Request(email, cpf), passwordHash);

    public static readonly DateOnly Today = new(2026, 10, 30);

    public static IBusinessClock Clock()
    {
        var clock = Substitute.For<IBusinessClock>();
        clock.Today.Returns(Today);
        return clock;
    }

    public static IUserRepository Repository(params User[] users)
    {
        var repository = Substitute.For<IUserRepository>();
        var data = users.AsQueryable();

        repository.Exists(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => data.Any(call.Arg<Expression<Func<User, bool>>>()));
        repository.Get(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => data.FirstOrDefault(call.Arg<Expression<Func<User, bool>>>()));
        repository.GetById(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => data.FirstOrDefault(u => u.Id == call.Arg<Guid>()));
        repository.GetByIdForUpdate(Arg.Any<Guid>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<CancellationToken>())
            .Returns(call => data.FirstOrDefault(u => u.Id == call.Arg<Guid>()));

        return repository;
    }

    /// <summary>Hash falso e previsível: <c>hash:senha</c>. O hash real tem teste próprio (BCrypt é lento de propósito).</summary>
    public static IPasswordHasher Hasher()
    {
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns(call => $"hash:{call.Arg<string>()}");
        hasher.Verify(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<string>(1) == $"hash:{call.ArgAt<string>(0)}");
        return hasher;
    }
}
