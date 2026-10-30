using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Seeders;

/// <summary>
/// Primeiro Admin, para conseguir logar num banco vazio. Só roda se não houver nenhum usuário.
/// Senha em <c>Seed:Admin:Password</c> (variável <c>Seed__Admin__Password</c>). Sem ela, Development usa a senha de dev,
/// que é pública no repositório; qualquer outro ambiente não sobe.
/// </summary>
public class UsersSeeder(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment) : ISeeder
{
    public const string AdminEmail = "admin@nextflow.com";
    private const string DevelopmentPassword = "!Admin123";

    public int Order => 10;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await userRepository.Exists(_ => true, ct))
            return;

        var dto = new UserRequestDto
        {
            Name = "Admin",
            LastName = "Nextflow",
            Cpf = "52998224725",
            BirthDate = new DateOnly(1990, 1, 1),
            Email = AdminEmail,
            Password = ResolvePassword(),
        };

        await userRepository.AddAsync(new User(dto, passwordHasher.Hash(dto.Password)), ct);
        await unitOfWork.CommitAsync(ct);
    }

    private string ResolvePassword()
    {
        var password = configuration["Seed:Admin:Password"];
        if (!string.IsNullOrWhiteSpace(password))
            return password;

        if (environment.IsDevelopment())
            return DevelopmentPassword;

        throw new InvalidOperationException(
            $"A configuração 'Seed:Admin:Password' é obrigatória em '{environment.EnvironmentName}' para criar o primeiro Admin. " +
            "Configure a variável de ambiente 'Seed__Admin__Password'.");
    }
}
