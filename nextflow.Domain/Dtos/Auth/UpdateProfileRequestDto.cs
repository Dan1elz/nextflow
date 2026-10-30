namespace Nextflow.Domain.Dtos.Auth;

/// <summary>Meu perfil (T-AC4): o usuário logado edita os próprios dados.</summary>
public class UpdateProfileRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
