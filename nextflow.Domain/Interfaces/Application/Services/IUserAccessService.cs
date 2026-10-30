namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Confere, a cada requisição autenticada, se o dono do token ainda pode usar a API: usuário inativado perde o acesso
/// na hora, não só no próximo login (RN-AC02, decisão de 29/10).
/// </summary>
public interface IUserAccessService
{
    Task<bool> CanAccess(Guid userId, CancellationToken ct = default);
}
