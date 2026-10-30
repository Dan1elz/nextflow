namespace Nextflow.Domain.Dtos.Users;

/// <summary>Criação de usuário (T-AC3). O perfil não vem do front: todo usuário é Admin (RN-AC04).</summary>
public class UserRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
