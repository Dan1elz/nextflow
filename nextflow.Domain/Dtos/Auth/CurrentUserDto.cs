using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Domain.Dtos.Auth;

/// <summary>
/// Usuário logado (<c>GET /api/auth/me</c>). <see cref="Stores"/> são as lojas do vínculo <c>user_stores</c>;
/// a última loja usada fica no navegador (decisão de 29/10, sem <c>lastStoreId</c> na API).
/// </summary>
public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<CurrentUserStoreDto> Stores { get; set; } = [];

    public CurrentUserDto() { }

    public CurrentUserDto(User entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        LastName = entity.LastName;
        Email = entity.Email;
        Role = entity.Role.GetApiName();
    }
}

public class CurrentUserStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public CurrentUserStoreDto() { }

    public CurrentUserStoreDto(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}
