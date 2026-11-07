using Nextflow.Application.Filters;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;

namespace Nextflow.Application.UseCases.Registry;

/// <summary>Ids resolvidos antes da consulta: o filtro entra no predicado como "id na lista" (ou fora dela).</summary>
internal sealed record IdFilter(List<Guid> Ids, bool Include);

/// <summary>
/// Filtros das listagens de clientes e fornecedores que dependem de outras tabelas (endereço, vendas, compras). O dono
/// não tem navegação para elas, então cada filtro vira uma lista de ids resolvida antes da paginação.
/// </summary>
internal static class RegistryListFilters
{
    public const string PurchasesKey = "purchases";

    /// <summary>
    /// <c>cityId</c> e <c>stateId</c>: donos com algum endereço na cidade ou na UF (os dois juntos = interseção).
    /// </summary>
    public static async Task<IdFilter?> Place(FilterSet set, IAddressRepository addresses, OwnerTypeEnum ownerType, CancellationToken ct)
    {
        List<Guid>? ids = null;
        if (set.TryGetString("cityId", out var rawCity) && Guid.TryParse(rawCity, out var cityId))
            ids = await addresses.GetOwnerIdsByCity(ownerType, cityId, ct);

        if (set.TryGetString("stateId", out var rawState) && Guid.TryParse(rawState, out var stateId))
        {
            var inState = await addresses.GetOwnerIdsByState(ownerType, stateId, ct);
            ids = ids is null ? inState : [.. ids.Intersect(inState)];
        }

        return ids is null ? null : new IdFilter(ids, true);
    }

    /// <summary>
    /// <c>purchases</c>: <c>with</c> (comprou alguma vez), <c>without</c> (nunca), <c>last30</c> e <c>last90</c> (comprou nos
    /// últimos N dias de negócio) e <c>inactive90</c> (já comprou, mas não nos últimos 90 dias).
    /// </summary>
    public static async Task<IdFilter?> Purchases(FilterSet set, IBusinessClock clock,
        Func<DateTime?, CancellationToken, Task<List<Guid>>> idsSince, CancellationToken ct)
    {
        if (!set.TryGetString(PurchasesKey, out var raw))
            return null;

        DateTime Since(int days) => clock.StartOfDayUtc(clock.Today.AddDays(-days));

        switch (raw)
        {
            case "with":
                return new IdFilter(await idsSince(null, ct), true);
            case "without":
                return new IdFilter(await idsSince(null, ct), false);
            case "last30":
                return new IdFilter(await idsSince(Since(30), ct), true);
            case "last90":
                return new IdFilter(await idsSince(Since(90), ct), true);
            case "inactive90":
                var recent = (await idsSince(Since(90), ct)).ToHashSet();
                return new IdFilter([.. (await idsSince(null, ct)).Where(id => !recent.Contains(id))], true);
            default:
                return null;
        }
    }
}
