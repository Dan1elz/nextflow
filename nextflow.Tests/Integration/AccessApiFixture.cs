using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Nextflow.Tests.Integration;

/// <summary>
/// A API inteira (Program.cs, JWT, middleware, seeders) sobre um Postgres só dela: o que os testes de use case não
/// enxergam, como o <c>OnTokenValidated</c> e a desserialização do corpo. O banco nasce com o Admin do seed.
/// </summary>
public sealed class AccessApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@nextflow.com";
    public const string AdminPassword = "!Admin123";
    public const string Password = "Senha123";

    private readonly PostgresFixture _postgres = new();
    private WebApplicationFactory<Program> _factory = null!;
    private int _sequence;

    public async Task InitializeAsync()
    {
        await _postgres.InitializeAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.ConnectionString);
            builder.UseSetting("Seed:Admin:Password", AdminPassword);
        });
    }

    public HttpClient Anonymous() => _factory.CreateClient();

    public HttpClient Authenticated(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<JsonElement> LoginAsync(string email, string password)
    {
        var response = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async Task<HttpClient> AdminAsync()
        => Authenticated((await LoginAsync(AdminEmail, AdminPassword)).GetProperty("token").GetString()!);

    /// <summary>Cria um usuário pela API, com e-mail e CPF que nenhum outro teste usa.</summary>
    public async Task<CreatedUser> CreateUserAsync(string password = Password)
    {
        var admin = await AdminAsync();
        var (email, cpf) = NewIdentity();
        var response = await admin.PostAsJsonAsync("/api/users", UserBody(email, cpf, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new CreatedUser(body.GetProperty("id").GetGuid(), email, cpf, password);
    }

    public (string Email, string Cpf) NewIdentity()
    {
        var number = Interlocked.Increment(ref _sequence);
        return ($"usuario{number}@nextflow.com", ValidCpf(100_000_000 + (number * 7)));
    }

    public static object UserBody(string email, string cpf, string password = Password) => new
    {
        name = "Maria",
        lastName = "Silva",
        cpf,
        birthDate = "1990-05-20",
        email,
        password,
    };

    private static string ValidCpf(int nineDigits)
    {
        var digits = nineDigits.ToString().Select(c => c - '0').ToList();
        for (var round = 0; round < 2; round++)
        {
            var sum = digits.Select((digit, index) => digit * (digits.Count + 1 - index)).Sum();
            var rest = sum % 11;
            digits.Add(rest < 2 ? 0 : 11 - rest);
        }
        return string.Concat(digits);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public sealed record CreatedUser(Guid Id, string Email, string Cpf, string Password);

[CollectionDefinition(Name)]
public class AccessApiCollection : ICollectionFixture<AccessApiFixture>
{
    public const string Name = "access-api";
}
