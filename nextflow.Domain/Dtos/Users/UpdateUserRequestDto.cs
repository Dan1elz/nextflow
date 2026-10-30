namespace Nextflow.Domain.Dtos.Users;

/// <summary>Edição de usuário (T-AC3): CPF não muda e a senha só troca se <see cref="NewPassword"/> vier preenchida.</summary>
public class UpdateUserRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? NewPassword { get; set; }
}
