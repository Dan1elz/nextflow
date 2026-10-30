using Nextflow.Domain.Models;

namespace Nextflow.Domain.Interfaces.Infrastructure.Services;

/// <summary>Emite o JWT de acesso: 8 h, sem refresh token nesta versão (L-AC4).</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(User user);
}
