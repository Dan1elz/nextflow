using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nextflow.Tests.Integration;

[Collection(AccessApiCollection.Name)]
public class AccessApiTests(AccessApiFixture api)
{
    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private static string[] FieldErrors(JsonElement body, string field)
        => [.. body.GetProperty("fieldErrors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!)];

    private async Task<HttpClient> SessionOf(CreatedUser user)
        => api.Authenticated((await api.LoginAsync(user.Email, user.Password)).GetProperty("token").GetString()!);

    private static object UpdateBody(string email, string? newPassword = null) => new
    {
        name = "Maria",
        lastName = "Souza",
        birthDate = "1990-05-20",
        email,
        newPassword,
    };

    private static StringContent RawJson(string json) => new(json, Encoding.UTF8, "application/json");

    // --- Autenticação (T-AC1)

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("GET", "/api/users")]
    [Trait("Rule", "RN-AC06")]
    [Trait("Rule", "L-AC1")]
    public async Task Should_Return401WithoutBody_When_CallingWithoutToken(string method, string uri)
    {
        var response = await api.Anonymous().SendAsync(new HttpRequestMessage(new HttpMethod(method), uri));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-AC06")]
    [Trait("Rule", "L-AC1")]
    public async Task Should_LoginAnonymouslyAndReadMe_When_UsingAuthRoutes()
    {
        var login = await api.LoginAsync(AccessApiFixture.AdminEmail, AccessApiFixture.AdminPassword);

        var me = await api.Authenticated(login.GetProperty("token").GetString()!).GetAsync("/api/auth/me");

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(me)).GetProperty("email").GetString().ShouldBe(AccessApiFixture.AdminEmail);
    }

    [Fact]
    [Trait("Rule", "RN-AC11")]
    public async Task Should_ReturnStoresAndNoLastStoreId_When_ReadingMe()
    {
        var body = await Json(await (await api.AdminAsync()).GetAsync("/api/auth/me"));

        body.TryGetProperty("lastStoreId", out _).ShouldBeFalse();
        body.GetProperty("stores").ValueKind.ShouldBe(JsonValueKind.Array);
    }

    [Fact]
    [Trait("Rule", "RN-AC03")]
    public async Task Should_ReturnSame401Body_When_EmailIsUnknownOrPasswordIsWrong()
    {
        var client = api.Anonymous();

        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = "ninguem@nextflow.com", password = "Senha123" });
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = AccessApiFixture.AdminEmail, password = "Errada123" });

        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsStringAsync()).ShouldBe(await wrong.Content.ReadAsStringAsync());
        (await Json(wrong)).GetProperty("message").GetString().ShouldBe("E-mail ou senha inválidos.");
    }

    [Fact]
    [Trait("Rule", "RN-AC02")]
    [Trait("Rule", "RN-AC03")]
    public async Task Should_Return403OnlyWithRightPassword_When_UserIsInactive()
    {
        var user = await api.CreateUserAsync();
        (await (await api.AdminAsync()).DeleteAsync($"/api/users/{user.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var client = api.Anonymous();

        var right = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });
        var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "Errada123" });

        right.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await Json(right)).GetProperty("message").GetString().ShouldBe("Usuário inativo.");
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Rule", "RN-AC10")]
    public async Task Should_Return401OnTheNextRequestAndAcceptAgain_When_UserIsInactivatedAndReactivated()
    {
        var user = await api.CreateUserAsync();
        var session = await SessionOf(user);
        var admin = await api.AdminAsync();
        (await session.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await admin.DeleteAsync($"/api/users/{user.Id}");

        var blocked = await session.GetAsync("/api/auth/me");
        blocked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await blocked.Content.ReadAsStringAsync()).ShouldBeEmpty();

        (await admin.PatchAsync($"/api/users/{user.Id}/reactivate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await session.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-AC10")]
    public async Task Should_Return401_When_TokenIsSignedWithAnotherKey()
    {
        var login = await api.LoginAsync(AccessApiFixture.AdminEmail, AccessApiFixture.AdminPassword);
        var token = login.GetProperty("token").GetString()!;

        var response = await api.Authenticated(token[..^4] + "AAAA").GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Rule", "RN-AC09")]
    [Trait("Rule", "L-AC4")]
    public async Task Should_ReturnTokenOfEightHoursAndNoRefreshToken_When_LoggingIn()
    {
        var login = await api.LoginAsync(AccessApiFixture.AdminEmail, AccessApiFixture.AdminPassword);

        var expiresAt = login.GetProperty("expiresAt").GetDateTime().ToUniversalTime();
        expiresAt.ShouldBe(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
        new JwtSecurityTokenHandler().ReadJwtToken(login.GetProperty("token").GetString()).ValidTo
            .ShouldBe(expiresAt, TimeSpan.FromSeconds(1));
        login.TryGetProperty("refreshToken", out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Rule", "RN-AC12")]
    [Trait("Rule", "L-AC5")]
    public async Task Should_ShowLastLoginOnListing_When_UserLogsIn()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = user.Email }));

        var before = (await Json(await admin.GetAsync($"/api/users?filters={filters}"))).GetProperty("items")[0];
        before.GetProperty("lastLoginAt").ValueKind.ShouldBe(JsonValueKind.Null);

        await api.LoginAsync(user.Email, user.Password);

        var after = (await Json(await admin.GetAsync($"/api/users?filters={filters}"))).GetProperty("items")[0];
        after.GetProperty("lastLoginAt").GetDateTime().ToUniversalTime().ShouldBe(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    // --- Usuários (T-AC2 e T-AC3)

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnTheField_When_EmailOrCpfAlreadyExistInAnyFormat()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        var (freshEmail, freshCpf) = api.NewIdentity();
        var maskedCpf = $"{user.Cpf[..3]}.{user.Cpf[3..6]}.{user.Cpf[6..9]}-{user.Cpf[9..]}";

        var sameEmail = await admin.PostAsJsonAsync("/api/users", AccessApiFixture.UserBody(user.Email.ToUpperInvariant(), freshCpf));
        var sameCpf = await admin.PostAsJsonAsync("/api/users", AccessApiFixture.UserBody(freshEmail, maskedCpf));

        sameEmail.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(sameEmail), "email").ShouldBe(["Já existe um usuário com este e-mail."]);
        sameCpf.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(sameCpf), "cpf").ShouldBe(["Já existe um usuário com este CPF."]);
    }

    [Fact]
    [Trait("Rule", "RN-AC01")]
    public async Task Should_Return422OnEmail_When_ProfileUsesEmailOfAnotherUser()
    {
        var user = await api.CreateUserAsync();
        var other = await api.CreateUserAsync();

        var response = await (await SessionOf(user)).PutAsJsonAsync("/api/auth/me", new { name = "Maria", lastName = "Silva", email = other.Email });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(response), "email").ShouldBe(["Já existe um usuário com este e-mail."]);
    }

    [Fact]
    [Trait("Rule", "RN-AC04")]
    public async Task Should_CreateAsAdmin_When_BodyTriesToSendAnotherRole()
    {
        var (email, cpf) = api.NewIdentity();

        var response = await (await api.AdminAsync()).PostAsync("/api/users", RawJson(
            $$"""{"name":"Maria","lastName":"Silva","cpf":"{{cpf}}","birthDate":"1990-05-20","email":"{{email}}","password":"Senha123","role":"User"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await Json(response)).GetProperty("role").GetString().ShouldBe("Admin");
    }

    [Fact]
    [Trait("Rule", "RN-AC05")]
    public async Task Should_NeverReturnPasswordOrHash_When_ReadingUsers()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();

        var bodies = new[]
        {
            await (await admin.GetAsync($"/api/users/{user.Id}")).Content.ReadAsStringAsync(),
            await (await admin.GetAsync("/api/users")).Content.ReadAsStringAsync(),
            await (await (await SessionOf(user)).GetAsync("/api/auth/me")).Content.ReadAsStringAsync(),
        };

        foreach (var body in bodies)
        {
            body.ShouldNotContain("password", Case.Insensitive);
            body.ShouldNotContain("$2");
        }
    }

    [Fact]
    public async Task Should_MaskCpf_When_Listing()
    {
        var user = await api.CreateUserAsync();
        var filters = Uri.EscapeDataString(JsonSerializer.Serialize(new { search = user.Email }));

        var body = await (await (await api.AdminAsync()).GetAsync($"/api/users?filters={filters}")).Content.ReadAsStringAsync();

        body.ShouldNotContain(user.Cpf);
    }

    [Fact]
    public async Task Should_FilterIgnoringCaseAndByStatus_When_Listing()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        await admin.DeleteAsync($"/api/users/{user.Id}");

        async Task<int> Count(object filter)
        {
            var filters = Uri.EscapeDataString(JsonSerializer.Serialize(filter));
            var body = await Json(await admin.GetAsync($"/api/users?filters={filters}"));
            return body.GetProperty("items").EnumerateArray().Count(i => i.GetProperty("id").GetGuid() == user.Id);
        }

        (await Count(new { search = user.Email.ToUpperInvariant() })).ShouldBe(1);
        (await Count(new { search = user.Email, isActive = "false" })).ShouldBe(1);
        (await Count(new { search = user.Email, isActive = "true" })).ShouldBe(0);
    }

    [Theory]
    [InlineData("fullName")]
    [InlineData("email")]
    [InlineData("createdAt")]
    [InlineData("lastLoginAt")]
    public async Task Should_Return200_When_SortingByAcceptedField(string sort)
    {
        var response = await (await api.AdminAsync()).GetAsync($"/api/users?sort={sort}&order=desc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-AC07")]
    [Trait("Rule", "L-AC2")]
    public async Task Should_Return400AndKeepActive_When_InactivatingOwnUser()
    {
        var user = await api.CreateUserAsync();
        var session = await SessionOf(user);

        var response = await session.DeleteAsync($"/api/users/{user.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(response)).GetProperty("message").GetString().ShouldBe("Você não pode inativar o seu próprio usuário.");
        (await session.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-AC07")]
    [Trait("Rule", "L-AC2")]
    public async Task Should_NeverLeaveCompanyWithoutActiveAdmin_When_AdminsInactivateEachOtherInSequence()
    {
        var first = await api.CreateUserAsync();
        var second = await api.CreateUserAsync();
        var firstSession = await SessionOf(first);
        var secondSession = await SessionOf(second);

        (await firstSession.DeleteAsync($"/api/users/{second.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Quem acabou de ser inativado não age mais (RN-AC10): em sequência, a recusa vem antes da regra do último Admin.
        (await secondSession.DeleteAsync($"/api/users/{first.Id}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await firstSession.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Rule", "RN-AC08")]
    [Trait("Rule", "L-AC3")]
    public async Task Should_ApplyPolicyOnCreateAndUpdateButNotOnLogin_When_PasswordIsWeak()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        var (email, cpf) = api.NewIdentity();

        var create = await admin.PostAsJsonAsync("/api/users", AccessApiFixture.UserBody(email, cpf, "fraca"));
        var update = await admin.PutAsJsonAsync($"/api/users/{user.Id}", UpdateBody(user.Email, "somenteletras"));
        var login = await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = "x" });

        create.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(create)).GetProperty("fieldErrors").TryGetProperty("password", out _).ShouldBeTrue();
        update.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await Json(update)).GetProperty("fieldErrors").TryGetProperty("newPassword", out _).ShouldBeTrue();
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Rule", "RN-AC08")]
    public async Task Should_AllowLoginWithNewPassword_When_AdminSetsItOnEdit()
    {
        var user = await api.CreateUserAsync();

        var update = await (await api.AdminAsync()).PutAsJsonAsync($"/api/users/{user.Id}", UpdateBody(user.Email, "Trocada123"));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await api.LoginAsync(user.Email, "Trocada123")).GetProperty("token").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Rule", "RN-AC13")]
    public async Task Should_Return400_When_EditingInactiveUser()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        await admin.DeleteAsync($"/api/users/{user.Id}");

        var response = await admin.PutAsJsonAsync($"/api/users/{user.Id}", UpdateBody(user.Email));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Json(await admin.GetAsync($"/api/users/{user.Id}"))).GetProperty("lastName").GetString().ShouldBe("Silva");
    }

    [Fact]
    [Trait("Rule", "RN-AC13")]
    public async Task Should_KeepCpf_When_UpdateBodyTriesToChangeIt()
    {
        var user = await api.CreateUserAsync();
        var admin = await api.AdminAsync();
        var (_, otherCpf) = api.NewIdentity();

        var response = await admin.PutAsync($"/api/users/{user.Id}", RawJson(
            $$"""{"name":"Maria","lastName":"Souza","birthDate":"1990-05-20","email":"{{user.Email}}","cpf":"{{otherCpf}}"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Json(await admin.GetAsync($"/api/users/{user.Id}"))).GetProperty("cpf").GetString().ShouldBe(user.Cpf);
    }

    // --- Meu perfil (T-AC4)

    [Fact]
    [Trait("Rule", "RN-AC14")]
    public async Task Should_ValidateCurrentPasswordAndSwitchIt_When_ChangingOwnPassword()
    {
        var user = await api.CreateUserAsync();
        var session = await SessionOf(user);

        var wrong = await session.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Errada123", newPassword = "Nova12345" });
        var same = await session.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = user.Password, newPassword = user.Password });
        var right = await session.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = user.Password, newPassword = "Nova12345" });
        var oldLogin = await api.Anonymous().PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = user.Password });

        wrong.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(wrong), "currentPassword").ShouldBe(["Senha atual incorreta."]);
        same.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(same), "newPassword").ShouldBe(["A nova senha precisa ser diferente da atual."]);
        right.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        oldLogin.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await api.LoginAsync(user.Email, "Nova12345")).GetProperty("token").GetString().ShouldNotBeNullOrEmpty();
    }

    // --- Corpo inválido: a desserialização falha antes do FluentValidation

    [Fact]
    public async Task Should_Return422OnBirthDate_When_DateCannotBeConverted()
    {
        var (email, cpf) = api.NewIdentity();

        var response = await (await api.AdminAsync()).PostAsync("/api/users", RawJson(
            $$"""{"name":"Maria","lastName":"Silva","cpf":"{{cpf}}","birthDate":"xx","email":"{{email}}","password":"Senha123"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await Json(response);
        body.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        FieldErrors(body, "birthDate").ShouldBe(["Valor inválido."]);
        body.GetProperty("fieldErrors").EnumerateObject().Select(field => field.Name).ShouldBe(["birthDate"]);
    }

    [Fact]
    public async Task Should_Return422OnBirthDate_When_UpdatingWithInvalidDate()
    {
        var user = await api.CreateUserAsync();

        var response = await (await api.AdminAsync()).PutAsync($"/api/users/{user.Id}", RawJson(
            $$"""{"name":"Maria","lastName":"Souza","birthDate":"2026-13-45","email":"{{user.Email}}"}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        FieldErrors(await Json(response), "birthDate").ShouldBe(["Valor inválido."]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ nao e json")]
    [InlineData("{\"email\":")]
    [InlineData("{\"birthDate\":\"xx\"")]
    [InlineData("[1,2]")]
    public async Task Should_Return400WithClearMessage_When_BodyIsEmptyOrUnreadable(string raw)
    {
        var response = await api.Anonymous().PostAsync("/api/auth/login", RawJson(raw));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await Json(response);
        body.GetProperty("isSuccess").GetBoolean().ShouldBeFalse();
        body.GetProperty("message").GetString()!.ShouldBe("O corpo da requisição está vazio ou não é um JSON válido.");
        body.GetProperty("fieldErrors").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_Return422ByFieldFromValidator_When_BodyIsValidJsonWithEmptyFields()
    {
        var response = await api.Anonymous().PostAsync("/api/auth/login", RawJson("{}"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await Json(response);
        FieldErrors(body, "email").ShouldContain("Informe o e-mail.");
        FieldErrors(body, "password").ShouldBe(["Informe a senha."]);
    }
}
