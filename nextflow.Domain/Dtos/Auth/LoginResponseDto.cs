namespace Nextflow.Domain.Dtos.Auth;

/// <summary>Resposta do login (T-AC1): token de 8 h sem refresh (L-AC4) e o usuário logado.</summary>
public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public CurrentUserDto User { get; set; } = new();

    public LoginResponseDto() { }

    public LoginResponseDto(string token, DateTime expiresAt, CurrentUserDto user)
    {
        Token = token;
        ExpiresAt = expiresAt;
        User = user;
    }
}
