using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Users;

/// <summary>Linha da listagem de usuários (T-AC2).</summary>
public class UserListItemDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CpfMasked { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    public UserListItemDto() { }

    public UserListItemDto(User entity)
    {
        Id = entity.Id;
        FullName = entity.FullName;
        Email = entity.Email;
        CpfMasked = entity.Cpf.MaskedPartially;
        Role = entity.Role.GetApiName();
        IsActive = entity.IsActive;
        CreatedAt = entity.CreatedAt;
        LastLoginAt = entity.LastLoginAt;
    }
}
