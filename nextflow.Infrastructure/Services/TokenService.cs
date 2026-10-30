using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Services;

public class TokenService(IConfiguration configuration) : ITokenService
{
    /// <summary>Duração do token (L-AC4): 8 h, sem refresh. Configurável em <c>JwtSettings:ExpirationHours</c>.</summary>
    public const int DefaultExpirationHours = 8;

    // Sem fallback para chave conhecida: a chave assinante vem da configuração/ambiente.
    private readonly string _secureKey = configuration["JwtSettings:Key"]
        ?? throw new InvalidOperationException(
            "A configuração 'JwtSettings:Key' é obrigatória. Configure a variável de ambiente 'JwtSettings__Key'.");

    private readonly int _expirationHours =
        int.TryParse(configuration["JwtSettings:ExpirationHours"], out var h) && h > 0 ? h : DefaultExpirationHours;

    public (string Token, DateTime ExpiresAt) GenerateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddHours(_expirationHours);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.GetApiName())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_secureKey)), SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expiresAt);
    }
}
