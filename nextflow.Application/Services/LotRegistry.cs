using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

/// <summary>
/// Acha ou cria o lote de uma chegada (F1.7). O código é único por variação (RN-CE10): o mesmo código com a mesma validade
/// é o mesmo lote (a mesma chegada em dois locais, ou uma reposição do mesmo lote do fornecedor); com outra validade, é
/// recusado. O lote novo entra na unidade de trabalho do chamador, que commita junto com o lançamento.
/// </summary>
public static class LotRegistry
{
    public static async Task<(Lot? Lot, string? Error)> FindOrCreate(
        ILotRepository repository, List<Lot> known, Guid variantId, string code, DateOnly expiresOn, CancellationToken ct)
    {
        var normalized = Lot.Normalize(code);
        var existing = known.FirstOrDefault(l => l.VariantId == variantId && l.Code == normalized);
        if (existing is not null)
        {
            return existing.ExpiresOn == expiresOn
                ? (existing, null)
                : (null, $"O lote {normalized} já existe nesta variação com validade {existing.ExpiresOn:dd/MM/yyyy}.");
        }

        var lot = new Lot(variantId, normalized, expiresOn);
        await repository.AddAsync(lot, ct);
        known.Add(lot);
        return (lot, null);
    }
}
