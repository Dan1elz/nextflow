using Microsoft.EntityFrameworkCore;
using Nextflow.Infrastructure.Database;
using Nextflow.Domain.Models;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Enums;

namespace Nextflow.Infrastructure.Seeders;

public class UsersSeeder(AppDbContext context) : ISeeder
{
    public int Order => 10;

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await context.Users.AnyAsync(ct))
        {
            return;
        }

        var users = new User[]
        {
            new(new CreateUserDto(){
                Name = "Admin User",
                LastName = "Admin",
                Email = "admin@nextflow.com",
                CPF = "00000000000",
                BirthDate = new DateOnly(1990, 1, 1),
                Password = "!Admin123",
            })
            {
                Role = RoleEnum.Admin
            },
        };

        context.Users.AddRange(users);

        await context.SaveChangesAsync(ct);
    }
}