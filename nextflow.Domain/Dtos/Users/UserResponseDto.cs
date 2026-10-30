using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Users;

/// <summary>Detalhe do usuário (T-AC3). Nunca carrega a senha (RN-AC05).</summary>
public class UserResponseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public UserResponseDto() { }

    public UserResponseDto(User entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        LastName = entity.LastName;
        Cpf = entity.Cpf.Value;
        BirthDate = entity.BirthDate;
        Email = entity.Email;
        Role = entity.Role.GetApiName();
        IsActive = entity.IsActive;
        CreatedAt = entity.CreatedAt;
        UpdatedAt = entity.UpdatedAt;
        LastLoginAt = entity.LastLoginAt;
    }
}
