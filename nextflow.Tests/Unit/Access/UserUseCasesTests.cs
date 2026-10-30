using Nextflow.Application.Services;
using Nextflow.Application.UseCases.Auth;
using Nextflow.Application.UseCases.Users;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;
using Nextflow.Domain.Validators.Auth;
using Nextflow.Domain.Validators.Users;
using NSubstitute;

namespace Nextflow.Tests.Unit.Access;

public class UserUseCasesTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private CreateUserUseCase Create(params User[] users) => new(
        AccessTestData.Repository(users), _unitOfWork, new UserRequestDtoValidator(AccessTestData.Clock()),
        new ResultFactory<UserResponseDto>(), AccessTestData.Hasher());

    private UpdateUserUseCase Update(params User[] users) => new(
        AccessTestData.Repository(users), _unitOfWork, new UpdateUserRequestDtoValidator(AccessTestData.Clock()),
        new ResultFactory<UserResponseDto>(), AccessTestData.Hasher());

    private UpdateProfileUseCase UpdateProfile(params User[] users) => new(
        AccessTestData.Repository(users), _unitOfWork, new UpdateProfileRequestDtoValidator(),
        new ResultFactory<CurrentUserDto>());

    private DeleteUserUseCase Delete(params User[] users) => new(
        AccessTestData.Repository(users), _unitOfWork, new ResultFactory<bool>());

    private ChangePasswordUseCase ChangePassword(params User[] users) => new(
        AccessTestData.Repository(users), _unitOfWork, new ChangePasswordRequestDtoValidator(),
        new ResultFactory<bool>(), AccessTestData.Hasher());

    private static UpdateUserRequestDto UpdateDto(string email = "maria@nextflow.com", string? newPassword = null) => new()
    {
        Name = "Maria",
        LastName = "Souza",
        BirthDate = new DateOnly(1990, 5, 20),
        Email = email,
        NewPassword = newPassword,
    };

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnEmailField_When_CreatingWithExistingEmail()
    {
        var existing = AccessTestData.NewUser();

        var result = await Create(existing).Execute(AccessTestData.Request(email: "MARIA@nextflow.com", cpf: AccessTestData.OtherValidCpf));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["email"].ShouldBe(["Já existe um usuário com este e-mail."]);
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnCpfField_When_CreatingWithExistingCpfInAnyFormat()
    {
        var existing = AccessTestData.NewUser();

        var result = await Create(existing).Execute(AccessTestData.Request(email: "joao@nextflow.com", cpf: "529.982.247-25"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["cpf"].ShouldBe(["Já existe um usuário com este CPF."]);
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnEmailField_When_UpdatingToEmailOfAnotherUser()
    {
        var user = AccessTestData.NewUser();
        var other = AccessTestData.NewUser(email: "joao@nextflow.com", cpf: AccessTestData.OtherValidCpf);

        var result = await Update(user, other).Execute(user.Id, UpdateDto(email: "joao@nextflow.com"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("email");
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Update_When_KeepingOwnEmail()
    {
        var user = AccessTestData.NewUser();

        var result = await Update(user).Execute(user.Id, UpdateDto());

        result.IsSuccess.ShouldBeTrue();
        user.LastName.ShouldBe("Souza");
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnEmailField_When_ProfileUsesEmailOfAnotherUser()
    {
        var user = AccessTestData.NewUser();
        var other = AccessTestData.NewUser(email: "joao@nextflow.com", cpf: AccessTestData.OtherValidCpf);

        var result = await UpdateProfile(user, other).Execute(user.Id, new UpdateProfileRequestDto
        {
            Name = "Maria",
            LastName = "Silva",
            Email = "joao@nextflow.com",
        });

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("email");
    }

    [Fact]
    [Trait("Rule", "RN-AC13")]
    public async Task Should_Return400AndKeepData_When_UpdatingInactiveUser()
    {
        var user = AccessTestData.NewUser();
        user.Delete();

        var result = await Update(user).Execute(user.Id, UpdateDto());

        result.ErrorCode.ShouldBe(400);
        user.LastName.ShouldBe("Silva");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "RN-AC13")]
    public void Should_NotAcceptCpf_When_UpdatingUser()
        => typeof(UpdateUserRequestDto).GetProperty("Cpf").ShouldBeNull();

    [Fact]
    [Trait("Rule", "RN-AC11")]
    public void Should_NotCarryLastStore_When_MappingCurrentUser()
        => typeof(CurrentUserDto).GetProperties().ShouldNotContain(p => p.Name.Contains("LastStore", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task Should_ActivateAgain_When_ReactivatingInactiveUser()
    {
        var user = AccessTestData.NewUser();
        user.Delete();
        var useCase = new ReactivateUserUseCase(AccessTestData.Repository(user), _unitOfWork, new ResultFactory<bool>());

        var result = await useCase.Execute(user.Id, Guid.NewGuid());

        result.IsSuccess.ShouldBeTrue();
        user.IsActive.ShouldBeTrue();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return400_When_ReactivatingActiveUser()
    {
        var user = AccessTestData.NewUser();
        var useCase = new ReactivateUserUseCase(AccessTestData.Repository(user), _unitOfWork, new ResultFactory<bool>());

        var result = await useCase.Execute(user.Id, Guid.NewGuid());

        result.ErrorCode.ShouldBe(400);
    }

    [Fact]
    [Trait("Rule", "RN-AC14")]
    public async Task Should_Return422OnNewPassword_When_NewPasswordEqualsCurrent()
    {
        var user = AccessTestData.NewUser(passwordHash: "hash:Atual1234");

        var result = await ChangePassword(user).Execute(user.Id, new ChangePasswordRequestDto
        {
            CurrentPassword = "Atual1234",
            NewPassword = "Atual1234",
        });

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["newPassword"].ShouldBe(["A nova senha precisa ser diferente da atual."]);
        user.PasswordHash.ShouldBe("hash:Atual1234");
    }

    [Fact]
    [Trait("Rule", "RN-AC04")]
    public async Task Should_CreateAsAdmin_When_CreatingAnyUser()
    {
        var result = await Create().Execute(AccessTestData.Request());

        result.Data!.Role.ShouldBe("Admin");
        AccessTestData.NewUser().Role.ShouldBe(RoleEnum.Admin);
        typeof(UserRequestDto).GetProperty("Role").ShouldBeNull();
        typeof(UpdateUserRequestDto).GetProperty("Role").ShouldBeNull();
    }

    [Fact]
    [Trait("Rule", "RN-AC05")]
    public async Task Should_StoreOnlyTheHash_When_CreatingUser()
    {
        var repository = AccessTestData.Repository();
        User? saved = null;
        await repository.AddAsync(Arg.Do<User>(u => saved = u), Arg.Any<CancellationToken>());
        var useCase = new CreateUserUseCase(
            repository, _unitOfWork, new UserRequestDtoValidator(AccessTestData.Clock()), new ResultFactory<UserResponseDto>(), AccessTestData.Hasher());

        await useCase.Execute(AccessTestData.Request());

        saved!.PasswordHash.ShouldBe($"hash:{AccessTestData.Password}");
    }

    [Fact]
    [Trait("Rule", "L-AC2")]
    [Trait("Rule", "RN-AC07")]
    public async Task Should_Return400AndKeepActive_When_InactivatingOwnUser()
    {
        var user = AccessTestData.NewUser();
        var other = AccessTestData.NewUser(email: "joao@nextflow.com", cpf: AccessTestData.OtherValidCpf);

        var result = await Delete(user, other).Execute(user.Id, user.Id);

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldBe("Você não pode inativar o seu próprio usuário.");
        user.IsActive.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "L-AC2")]
    [Trait("Rule", "RN-AC07")]
    public async Task Should_Return400AndKeepActive_When_InactivatingLastActiveAdmin()
    {
        var lastAdmin = AccessTestData.NewUser();
        var inactiveAdmin = AccessTestData.NewUser(email: "joao@nextflow.com", cpf: AccessTestData.OtherValidCpf);
        inactiveAdmin.Delete();

        var result = await Delete(lastAdmin, inactiveAdmin).Execute(lastAdmin.Id, Guid.NewGuid());

        result.ErrorCode.ShouldBe(400);
        result.Message.ShouldBe("Não é possível inativar o último administrador ativo.");
        lastAdmin.IsActive.ShouldBeTrue();
    }

    [Fact]
    [Trait("Rule", "L-AC2")]
    [Trait("Rule", "RN-AC07")]
    public async Task Should_Inactivate_When_AnotherActiveAdminRemains()
    {
        var actor = AccessTestData.NewUser();
        var target = AccessTestData.NewUser(email: "joao@nextflow.com", cpf: AccessTestData.OtherValidCpf);

        var result = await Delete(actor, target).Execute(target.Id, actor.Id);

        result.IsSuccess.ShouldBeTrue();
        target.IsActive.ShouldBeFalse();
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    public async Task Should_Return422OnNewPassword_When_UpdatingWithWeakPassword()
    {
        var user = AccessTestData.NewUser();

        var result = await Update(user).Execute(user.Id, UpdateDto(newPassword: "fraca"));

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!.ShouldContainKey("newPassword");
        user.PasswordHash.ShouldBe("hash");
    }

    [Fact]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    public async Task Should_KeepPassword_When_UpdatingWithoutNewPassword()
    {
        var user = AccessTestData.NewUser();

        await Update(user).Execute(user.Id, UpdateDto());

        user.PasswordHash.ShouldBe("hash");
    }

    [Fact]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    public async Task Should_ChangePassword_When_CurrentPasswordIsRight()
    {
        var user = AccessTestData.NewUser(passwordHash: "hash:Atual1234");

        var result = await ChangePassword(user).Execute(user.Id, new ChangePasswordRequestDto
        {
            CurrentPassword = "Atual1234",
            NewPassword = "Nova12345",
        });

        result.IsSuccess.ShouldBeTrue();
        user.PasswordHash.ShouldBe("hash:Nova12345");
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    [Trait("Rule", "RN-AC14")]
    public async Task Should_Return422OnCurrentPassword_When_CurrentPasswordIsWrong()
    {
        var user = AccessTestData.NewUser(passwordHash: "hash:Atual1234");

        var result = await ChangePassword(user).Execute(user.Id, new ChangePasswordRequestDto
        {
            CurrentPassword = "Errada123",
            NewPassword = "Nova12345",
        });

        result.ErrorCode.ShouldBe(422);
        result.FieldErrors!["currentPassword"].ShouldBe(["Senha atual incorreta."]);
        user.PasswordHash.ShouldBe("hash:Atual1234");
    }
}
