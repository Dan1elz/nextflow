using FluentValidation.Results;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

/// <summary>
/// Livro-razão de estoque (ADR 0007). Três passadas: a primeira resolve o lote de cada linha (F1.7: entrada de produto com
/// lote exige o lote; saída sem lote escolhido é repartida pelo lote que vence primeiro, FEFO, RN-CE15); a segunda projeta os
/// saldos e recusa o que deixaria negativo (RN-CE32) sem tocar em nada; a terceira grava saldo, custo médio e movimentação.
/// As linhas valem na ordem: uma saída depois de uma entrada na mesma chave enxerga a entrada.
/// </summary>
public class StockLedger(
    IStockBalanceRepository balanceRepository,
    IStockMovementRepository movementRepository,
    IProductVariantRepository variantRepository,
    IProductRepository productRepository,
    IStockLocationRepository locationRepository,
    ILotRepository lotRepository) : IStockLedger
{
    public const string NegativeMessage = "Saldo insuficiente: o produto não permite estoque negativo.";
    public const string LotRequiredMessage = "Informe o lote: o produto controla lote e validade.";
    public const string LotNotTrackedMessage = "Este produto não controla lote.";
    public const string LotNotFoundMessage = "Lote não encontrado nesta variação.";
    public const string NoLotYetMessage = "Esta variação ainda não recebeu nenhum lote: lance uma entrada com lote antes da saída.";

    /// <summary>Linha já com o lote resolvido; <see cref="Index"/> é a linha original, para a falha voltar no campo certo.</summary>
    private sealed record ResolvedLine(int Index, StockLineDto Line);

    public async Task<List<ValidationFailure>> Post(StockDocumentDto document, IReadOnlyList<StockLineDto> lines, CancellationToken ct)
        => (await PostResolved(document, lines, ct)).Failures;

    public async Task<(List<ValidationFailure> Failures, List<StockLineDto> Posted)> PostResolved(
        StockDocumentDto document, IReadOnlyList<StockLineDto> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
            return ([new("Lines", "Informe ao menos uma linha.")], []);

        var variantIds = lines.Select(l => l.VariantId).Distinct().ToList();
        var locationIds = lines.Select(l => l.LocationId).Distinct().ToList();

        var variants = (await variantRepository.GetAllForUpdate(v => variantIds.Contains(v.Id), ct)).ToDictionary(v => v.Id);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var products = (await productRepository.GetAll(p => productIds.Contains(p.Id), 0, int.MaxValue, ct: ct)).ToDictionary(p => p.Id);
        var openLocations = (await locationRepository.GetAll(
            l => locationIds.Contains(l.Id) && l.IsActive && l.Store.IsActive, 0, int.MaxValue, ct: ct))
            .Select(l => l.Id).ToHashSet();
        var balances = await balanceRepository.GetAllForUpdate(
            b => variantIds.Contains(b.VariantId) && locationIds.Contains(b.LocationId), ct);
        var lots = (await lotRepository.GetForVariants(variantIds, ct)).ToDictionary(l => l.Id);

        var failures = new List<ValidationFailure>();
        var resolved = ResolveLots(lines, variants, products, lots, balances, failures);
        failures.AddRange(Validate(resolved, variants, products, openLocations, balances));
        if (failures.Count > 0)
            return ([.. failures.DistinctBy(f => (f.PropertyName, f.ErrorMessage))], []);

        var companyQuantities = await balanceRepository.SumByVariant(variantIds, ct);

        foreach (var line in resolved.Select(r => r.Line))
        {
            var variant = variants[line.VariantId];
            var balance = balances.FirstOrDefault(b => SameKey(b, line));
            if (balance is null)
            {
                balance = new StockBalance(line);
                balances.Add(balance);
                await balanceRepository.AddAsync(balance, ct);
            }

            var signed = StockMovement.Signed(line);
            var companyBefore = companyQuantities.GetValueOrDefault(line.VariantId);

            // A saída registra o custo médio vigente (base do custo da mercadoria vendida); a entrada, o custo informado.
            // O médio é da variação, não do lote: cada lote de uma entrada recalcula sobre o saldo da empresa (RN-CE31).
            var unitCost = line.Type == MovementTypeEnum.Out ? variant.AverageCost : line.UnitCost;
            if (line.Type == MovementTypeEnum.In && line.UnitCost is { } cost)
                variant.ApplyInboundCost(companyBefore, line.Quantity, cost);

            balance.Apply(signed);
            companyQuantities[line.VariantId] = companyBefore + signed;

            await movementRepository.AddAsync(new StockMovement(document, line, unitCost, balance.Quantity), ct);
        }

        return ([], [.. resolved.Select(r => r.Line)]);
    }

    /// <summary>
    /// Produto sem lote: a linha segue sem lote (lote informado é recusado). Produto com lote: lote informado precisa ser da
    /// variação; entrada sem lote é recusada; saída sem lote é repartida entre os lotes com saldo no local, do que vence
    /// primeiro ao último (FEFO, RN-CE15), vencidos inclusive, que saem antes. Se os lotes não cobrem a saída, o produto que
    /// não permite negativo é recusado; o que permite joga o resto no primeiro lote da fila (sem saldo no local, no lote da
    /// variação que vence primeiro; sem nenhum lote na variação, recusa).
    /// </summary>
    private static List<ResolvedLine> ResolveLots(
        IReadOnlyList<StockLineDto> lines,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, Product> products,
        Dictionary<Guid, Lot> lots,
        List<StockBalance> balances,
        List<ValidationFailure> failures)
    {
        var resolved = new List<ResolvedLine>();
        var projected = new Dictionary<(Guid, Guid, Guid?), decimal>();

        decimal Current(Guid variantId, Guid locationId, Guid? lotId)
            => projected.TryGetValue((variantId, locationId, lotId), out var value)
                ? value
                : balances.FirstOrDefault(b => b.VariantId == variantId && b.LocationId == locationId && b.LotId == lotId)?.Quantity ?? 0;

        void Add(int index, StockLineDto line)
        {
            resolved.Add(new(index, line));
            projected[(line.VariantId, line.LocationId, line.LotId)] =
                Current(line.VariantId, line.LocationId, line.LotId) + StockMovement.Signed(line);
        }

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!variants.TryGetValue(line.VariantId, out var variant) || !products.TryGetValue(variant.ProductId, out var product))
            {
                resolved.Add(new(i, line));
                continue;
            }

            if (!product.TracksLots)
            {
                if (line.LotId is not null)
                    failures.Add(new($"Lines[{i}].LotId", LotNotTrackedMessage));
                Add(i, line);
                continue;
            }

            if (line.LotId is { } lotId)
            {
                if (!lots.TryGetValue(lotId, out var lot) || lot.VariantId != line.VariantId)
                    failures.Add(new($"Lines[{i}].LotId", LotNotFoundMessage));
                Add(i, line);
                continue;
            }

            if (line.Type == MovementTypeEnum.In)
            {
                failures.Add(new($"Lines[{i}].LotId", LotRequiredMessage));
                resolved.Add(new(i, line));
                continue;
            }

            if (line.Quantity <= 0)
            {
                resolved.Add(new(i, line));
                continue;
            }

            var queue = lots.Values
                .Where(l => l.VariantId == line.VariantId)
                .OrderBy(l => l.ExpiresOn).ThenBy(l => l.Code).ThenBy(l => l.Id)
                .Select(l => (Lot: l, Quantity: Current(line.VariantId, line.LocationId, l.Id)))
                .Where(c => c.Quantity > 0)
                .ToList();

            var remaining = line.Quantity;
            foreach (var (lot, quantity) in queue)
            {
                if (remaining <= 0)
                    break;
                var take = Math.Min(remaining, quantity);
                Add(i, WithLot(line, lot.Id, take));
                remaining -= take;
            }

            if (remaining <= 0)
                continue;

            if (!product.AllowsNegative)
            {
                failures.Add(new($"Lines[{i}].Quantity", $"{NegativeMessage} Saldo nos lotes do local: {queue.Sum(c => c.Quantity):0.###}."));
                continue;
            }

            // O resto nunca fica sem lote: um saldo sem lote de produto com lote não teria como zerar (a entrada exige lote
            // e a saída vai por FEFO). Vai para o primeiro lote da fila; sem saldo em lote no local, para o lote da
            // variação que vence primeiro; variação que nunca recebeu lote é recusada.
            var fallback = queue.Count > 0
                ? queue[0].Lot.Id
                : lots.Values
                    .Where(l => l.VariantId == line.VariantId)
                    .OrderBy(l => l.ExpiresOn).ThenBy(l => l.Code).ThenBy(l => l.Id)
                    .Select(l => (Guid?)l.Id)
                    .FirstOrDefault();
            if (fallback is null)
            {
                failures.Add(new($"Lines[{i}].LotId", NoLotYetMessage));
                continue;
            }

            Add(i, WithLot(line, fallback, remaining));
        }

        return resolved;
    }

    private static StockLineDto WithLot(StockLineDto line, Guid? lotId, decimal quantity) => new()
    {
        VariantId = line.VariantId,
        LocationId = line.LocationId,
        LotId = lotId,
        Type = line.Type,
        Reason = line.Reason,
        Quantity = quantity,
        UnitCost = line.UnitCost,
    };

    private static List<ValidationFailure> Validate(
        List<ResolvedLine> lines,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, Product> products,
        HashSet<Guid> openLocations,
        List<StockBalance> balances)
    {
        var failures = new List<ValidationFailure>();
        var projected = new Dictionary<(Guid, Guid, Guid?), decimal>();

        foreach (var (i, line) in lines)
        {
            if (line.Quantity <= 0)
                failures.Add(new($"Lines[{i}].Quantity", "A quantidade precisa ser maior que zero."));
            if (line.UnitCost is < 0m)
                failures.Add(new($"Lines[{i}].UnitCost", "O custo não pode ser negativo."));
            if (!openLocations.Contains(line.LocationId))
                failures.Add(new($"Lines[{i}].LocationId", "Escolha um local ativo de uma loja ativa."));
            if (!variants.TryGetValue(line.VariantId, out var variant) || !products.TryGetValue(variant.ProductId, out var product))
            {
                failures.Add(new($"Lines[{i}].VariantId", "Variação não encontrada."));
                continue;
            }

            if (line.Quantity <= 0)
                continue;

            var key = (line.VariantId, line.LocationId, line.LotId);
            var current = projected.TryGetValue(key, out var value)
                ? value
                : balances.FirstOrDefault(b => SameKey(b, line))?.Quantity ?? 0;
            var next = current + StockMovement.Signed(line);

            // Só a saída que leva para baixo de zero é barrada: entrada num saldo já negativo sempre passa (RN-CE32).
            if (line.Type == MovementTypeEnum.Out && next < 0 && !product.AllowsNegative)
                failures.Add(new($"Lines[{i}].Quantity", $"{NegativeMessage} Saldo no local: {current:0.###}."));

            projected[key] = next;
        }

        return failures;
    }

    private static bool SameKey(StockBalance balance, StockLineDto line)
        => balance.VariantId == line.VariantId && balance.LocationId == line.LocationId && balance.LotId == line.LotId;
}
