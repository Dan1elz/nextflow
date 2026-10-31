using Nextflow.Domain.Dtos.Auth;

namespace Nextflow.Domain.Interfaces.Application.Services;

/// <summary>
/// Mantém <c>user_stores</c> (decisão 6 de 29/10): todo Admin ativo enxerga toda loja ativa. Só registra os vínculos que
/// faltam, no commit do use case que chamou.
/// </summary>
public interface IStoreMembership
{
    /// <summary>Vincula a loja a todos os Admins ativos.</summary>
    Task LinkStore(Guid storeId, CancellationToken ct = default);

    /// <summary>Vincula o usuário a todas as lojas ativas.</summary>
    Task LinkUser(Guid userId, CancellationToken ct = default);

    /// <summary>Lojas ativas do usuário, por nome.</summary>
    Task<List<CurrentUserStoreDto>> GetStoresOf(Guid userId, CancellationToken ct = default);
}
