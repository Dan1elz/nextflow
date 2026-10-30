using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Nextflow.Application.Services;
using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Validators.Auth;
using Nextflow.Domain.Validators.Users;
using Nextflow.Infrastructure.Services;
using NSubstitute;

namespace Nextflow.Tests.Unit.Access;

public class AccessRulesTests
{
    private const string TestJwtKey = "chave-de-teste-com-mais-de-32-caracteres-0001";

    private static TokenService TokenService(params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string?> { ["JwtSettings:Key"] = TestJwtKey };
        foreach (var (key, value) in extra)
            values[key] = value;
        return new TokenService(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    [Fact]
    [Trait("Rule", "RN-AC02")]
    [Trait("Rule", "RN-AC10")]
    public async Task Should_DenyAccess_When_UserWasInactivatedAfterGettingTheToken()
    {
        var user = AccessTestData.NewUser();
        var service = new UserAccessService(AccessTestData.Repository(user));

        (await service.CanAccess(user.Id)).ShouldBeTrue();

        user.Delete();

        (await service.CanAccess(user.Id)).ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-AC02")]
    public async Task Should_DenyAccess_When_UserDoesNotExist()
    {
        var service = new UserAccessService(AccessTestData.Repository());

        (await service.CanAccess(Guid.NewGuid())).ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(UserResponseDto))]
    [InlineData(typeof(UserListItemDto))]
    [InlineData(typeof(CurrentUserDto))]
    [Trait("Rule", "RN-AC05")]
    public void Should_NotExposePassword_When_MappingUserToResponse(Type dto)
        => dto.GetProperties().ShouldNotContain(p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));

    [Fact]
    [Trait("Rule", "RN-AC05")]
    public void Should_NotCarryTheHash_When_SerializingResponses()
    {
        var user = AccessTestData.NewUser(passwordHash: "hash-secreto");

        JsonConvert.SerializeObject(new UserResponseDto(user)).ShouldNotContain("hash-secreto");
        JsonConvert.SerializeObject(new UserListItemDto(user)).ShouldNotContain("hash-secreto");
        JsonConvert.SerializeObject(new LoginResponseDto("t", DateTime.UtcNow, new CurrentUserDto(user))).ShouldNotContain("hash-secreto");
    }

    [Fact]
    [Trait("Rule", "RN-AC05")]
    public void Should_HashAndVerifyWithBcrypt_When_UsingPasswordHasher()
    {
        var hasher = new PasswordHasher();

        var hash = hasher.Hash(AccessTestData.Password);

        hash.ShouldNotContain(AccessTestData.Password);
        hash.ShouldStartWith("$2");
        hasher.Verify(AccessTestData.Password, hash).ShouldBeTrue();
        hasher.Verify("Errada123", hash).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Curta12", false)]        // 7 caracteres
    [InlineData("somenteletras", false)]  // sem número
    [InlineData("1234567890", false)]     // sem letra
    [InlineData("", false)]
    [InlineData("senha123", true)]        // 8, letra e número: maiúscula e símbolo não são exigidos
    [InlineData("!Admin123", true)]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    public void Should_ApplyPasswordPolicy_When_ValidatingUserCreation(string password, bool valid)
    {
        var result = new UserRequestDtoValidator(AccessTestData.Clock()).Validate(AccessTestData.Request(password: password));

        result.Errors.Any(e => e.PropertyName == nameof(UserRequestDto.Password)).ShouldBe(!valid);
    }

    [Fact]
    [Trait("Rule", "L-AC3")]
    [Trait("Rule", "RN-AC08")]
    public void Should_RejectNewPassword_When_ChangingToWeakOrSamePassword()
    {
        var validator = new ChangePasswordRequestDtoValidator();

        validator.Validate(new ChangePasswordRequestDto { CurrentPassword = "Atual1234", NewPassword = "fraca" }).IsValid.ShouldBeFalse();
        validator.Validate(new ChangePasswordRequestDto { CurrentPassword = "Atual1234", NewPassword = "Atual1234" }).IsValid.ShouldBeFalse();
        validator.Validate(new ChangePasswordRequestDto { CurrentPassword = "Atual1234", NewPassword = "Nova12345" }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Should_RejectBirthDate_When_ItIsTodayOrFutureInBusinessDate()
    {
        var validator = new UserRequestDtoValidator(AccessTestData.Clock());
        var update = new UpdateUserRequestDtoValidator(AccessTestData.Clock());
        var dto = AccessTestData.Request();
        var updateDto = new UpdateUserRequestDto { Name = "Maria", LastName = "Silva", Email = "maria@nextflow.com" };

        foreach (var (date, valid) in new[] { (AccessTestData.Today.AddDays(-1), true), (AccessTestData.Today, false), (AccessTestData.Today.AddDays(1), false) })
        {
            dto.BirthDate = date;
            updateDto.BirthDate = date;
            validator.Validate(dto).Errors.Any(e => e.PropertyName == nameof(UserRequestDto.BirthDate)).ShouldBe(!valid);
            update.Validate(updateDto).Errors.Any(e => e.PropertyName == nameof(UpdateUserRequestDto.BirthDate)).ShouldBe(!valid);
        }
    }

    [Fact]
    public void Should_ReadTodayAtValidation_When_ValidatorIsReused()
    {
        var clock = AccessTestData.Clock();
        var validator = new UserRequestDtoValidator(clock);
        var dto = AccessTestData.Request();
        dto.BirthDate = AccessTestData.Today;

        validator.Validate(dto).Errors.ShouldContain(e => e.PropertyName == nameof(UserRequestDto.BirthDate));

        clock.Today.Returns(AccessTestData.Today.AddDays(1));

        validator.Validate(dto).Errors.ShouldNotContain(e => e.PropertyName == nameof(UserRequestDto.BirthDate));
    }

    [Fact]
    public void Should_RejectCpf_When_CheckDigitsAreWrong()
    {
        var result = new UserRequestDtoValidator(AccessTestData.Clock()).Validate(AccessTestData.Request(cpf: "52998224700"));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(UserRequestDto.Cpf));
    }

    [Fact]
    [Trait("Rule", "L-AC4")]
    [Trait("Rule", "RN-AC09")]
    public void Should_ExpireInEightHours_When_GeneratingTokenWithDefaultSettings()
    {
        var before = DateTime.UtcNow;

        var (token, expiresAt) = TokenService().GenerateAccessToken(AccessTestData.NewUser());

        expiresAt.ShouldBeInRange(before.AddHours(8).AddSeconds(-1), DateTime.UtcNow.AddHours(8).AddSeconds(1));
        new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo
            .ShouldBe(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    [Trait("Rule", "L-AC4")]
    [Trait("Rule", "RN-AC09")]
    public void Should_NotCarryPasswordOrCpf_When_GeneratingToken()
    {
        var user = AccessTestData.NewUser(passwordHash: "hash-secreto");

        var (token, _) = TokenService().GenerateAccessToken(user);

        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token).Payload.SerializeToJson();
        payload.ShouldNotContain("hash-secreto");
        payload.ShouldNotContain(AccessTestData.ValidCpf);
    }
}
