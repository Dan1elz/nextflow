using System.Security.Claims;

namespace Nextflow.Utils;

public static class TokenHelper
{
    public static Guid GetUserId(ClaimsPrincipal? user)
    {
        if (user == null || !user.Identity?.IsAuthenticated == true)
            throw new InvalidOperationException("Usuário não autenticado.");

        string? userIdClaim = user.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        return !Guid.TryParse(userIdClaim, out Guid userId)
            ? throw new InvalidOperationException("O ID do usuário no token é inválido ou não está presente.")
            : userId;
    }
}
