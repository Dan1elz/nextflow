using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Auth;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Auth;
using NSubstitute;

namespace Nextflow.Tests.Unit.Access;

public class LoginUseCaseTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private LoginUseCase UseCase(params User[] users)
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateAccessToken(Arg.Any<User>()).Returns(("token", Now.AddHours(8)));
        var clock = Substitute.For<IBusinessClock>();
        clock.UtcNow.Returns(Now);

        return new LoginUseCase(
            AccessTestData.Repository(users), _unitOfWork, new LoginRequestDtoValidator(),
            new ResultFactory<LoginResponseDto>(), AccessTestData.Hasher(), tokenService, clock);
    }

    private static User UserWithPassword() => AccessTestData.NewUser(passwordHash: $"hash:{AccessTestData.Password}");

    private static LoginRequestDto Credentials(string email = "maria@nextflow.com", string password = AccessTestData.Password)
        => new() { Email = email, Password = password };

    [Fact]
    public async Task Should_ReturnTokenAndUser_When_CredentialsAreValid()
    {
        var result = await UseCase(UserWithPassword()).Execute(Credentials());

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Token.ShouldBe("token");
        result.Data.ExpiresAt.ShouldBe(Now.AddHours(8));
        result.Data.User.Email.ShouldBe("maria@nextflow.com");
    }

    [Fact]
    public async Task Should_FindUser_When_EmailHasDifferentCaseAndSpaces()
    {
        var result = await UseCase(UserWithPassword()).Execute(Credentials(email: "  MARIA@nextflow.com "));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "RN-AC02")]
    public async Task Should_Return403AndNotRegisterAccess_When_UserIsInactive()
    {
        var user = UserWithPassword();
        user.Delete();

        var result = await UseCase(user).Execute(Credentials());

        result.ErrorCode.ShouldBe(403);
        result.Message.ShouldBe("Usuário inativo.");
        user.LastLoginAt.ShouldBeNull();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-AC03")]
    public async Task Should_ReturnSameGenericMessage_When_EmailDoesNotExistOrPasswordIsWrong()
    {
        var unknownEmail = await UseCase(UserWithPassword()).Execute(Credentials(email: "outra@nextflow.com"));
        var wrongPassword = await UseCase(UserWithPassword()).Execute(Credentials(password: "Errada123"));

        unknownEmail.ErrorCode.ShouldBe(401);
        wrongPassword.ErrorCode.ShouldBe(401);
        unknownEmail.Message.ShouldBe("E-mail ou senha inválidos.");
        wrongPassword.Message.ShouldBe(unknownEmail.Message);
    }

    [Fact]
    [Trait("Rule", "RN-AC03")]
    public async Task Should_NotRevealInactiveUser_When_PasswordIsWrong()
    {
        var user = UserWithPassword();
        user.Delete();

        var result = await UseCase(user).Execute(Credentials(password: "Errada123"));

        result.ErrorCode.ShouldBe(401);
        result.Message.ShouldBe("E-mail ou senha inválidos.");
    }

    [Fact]
    public async Task Should_Return422ByField_When_EmailIsInvalid()
    {
        var result = await UseCase().Execute(Credentials(email: "nao-e-email"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("email");
    }

    [Fact]
    [Trait("Rule", "L-AC5")]
    [Trait("Rule", "RN-AC12")]
    public async Task Should_RegisterLastLoginAndCommitOnce_When_LoginSucceeds()
    {
        var user = UserWithPassword();

        await UseCase(user).Execute(Credentials());

        user.LastLoginAt.ShouldBe(Now);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "L-AC5")]
    [Trait("Rule", "RN-AC12")]
    public async Task Should_NotRegisterLastLogin_When_PasswordIsWrong()
    {
        var user = UserWithPassword();

        await UseCase(user).Execute(Credentials(password: "Errada123"));

        user.LastLoginAt.ShouldBeNull();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
