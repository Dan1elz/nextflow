using Nextflow.Domain.Dtos.Auth;
using Nextflow.Domain.Dtos.Users;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;
using Nextflow.Domain.ValueObjects;

namespace Nextflow.Domain.Models;

public class User : BaseModel, IDeletable, IUpdatable<UpdateUserRequestDto>
{
    public string Name { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public TaxDocument Cpf { get; private set; } = null!;
    public DateOnly BirthDate { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public RoleEnum Role { get; private set; } = RoleEnum.Admin;
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public string FullName => $"{Name} {LastName}".Trim();

    public override string Preposition => "o";
    public override string Singular => "usuário";
    public override string Plural => "usuários";

    private User() { }

    /// <summary>Todo usuário nasce Admin enquanto só existir esse perfil (RN-AC04, ADR 0004).</summary>
    public User(UserRequestDto dto, string passwordHash)
    {
        Name = dto.Name.Trim();
        LastName = dto.LastName.Trim();
        Cpf = TaxDocument.ParseCpf(dto.Cpf);
        BirthDate = dto.BirthDate;
        Email = NormalizeEmail(dto.Email);
        PasswordHash = passwordHash;
        Role = RoleEnum.Admin;
    }

    public void Update(UpdateUserRequestDto dto)
    {
        Name = dto.Name.Trim();
        LastName = dto.LastName.Trim();
        BirthDate = dto.BirthDate;
        Email = NormalizeEmail(dto.Email);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(UpdateProfileRequestDto dto)
    {
        Name = dto.Name.Trim();
        LastName = dto.LastName.Trim();
        Email = NormalizeEmail(dto.Email);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Último acesso (L-AC5), gravado a cada login com sucesso.</summary>
    public void RegisterLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void Delete()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
