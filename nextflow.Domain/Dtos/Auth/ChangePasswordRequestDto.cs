namespace Nextflow.Domain.Dtos.Auth;

/// <summary>Troca da própria senha (T-AC4). A confirmação da nova senha é só do formulário.</summary>
public class ChangePasswordRequestDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
