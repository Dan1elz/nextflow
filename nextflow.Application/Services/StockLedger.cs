using FluentValidation.Results;
using Nextflow.Domain.Dtos.Stock;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Models;

namespace Nextflow.Application.Services;

/// <summary>
/// Livro-razão de estoque (ADR 0007). Duas passadas: a primeira projeta os saldos e recusa o que deixaria negativo
/// (RN-CE32) sem tocar em nada; a segunda grava saldo, custo médio e movimentação. As linhas valem na ordem: uma saída depois
/// de uma entrada na mesma chave enxerga a entrada.
/// </summary>
public class StockLedger(
    IStockBalanceRepository balanceRepository,
    IStockMovementRepository movementRepository,
    IProductVariantRepository variantRepository,
    IProductRepository productRepository,
    IStockLocationRepository locationRepository) : IStockLedger
{
    public const string NegativeMessage = "Saldo insuficiente: o produto não permite estoque negativo.";

    public async Task<List<ValidationFailure>> Post(StockDocumentDto document, IReadOnlyList<StockLineDto> lines, CancellationToken ct)
    {
        if (lines.Count == 0)
            return [new("Lines", "Informe ao menos uma linha.")];

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

        var failures = Validate(lines, variants, products, openLocations, balances);
        if (failures.Count > 0)
            return failures;

        var companyQuantities = await balanceRepository.SumByVariant(variantIds, ct);

        foreach (var line in lines)
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
            var unitCost = line.Type == MovementTypeEnum.Out ? variant.AverageCost : line.UnitCost;
            if (line.Type == MovementTypeEnum.In && line.UnitCost is { } cost)
                variant.ApplyInboundCost(companyBefore, line.Quantity, cost);

            balance.Apply(signed);
            companyQuantities[line.VariantId] = companyBefore + signed;

            await movementRepository.AddAsync(new StockMovement(document, line, unitCost, balance.Quantity), ct);
        }

        return [];
    }

    private static List<ValidationFailure> Validate(
        IReadOnlyList<StockLineDto> lines,
        Dictionary<Guid, ProductVariant> variants,
        Dictionary<Guid, Product> products,
        HashSet<Guid> openLocations,
        List<StockBalance> balances)
    {
        var failures = new List<ValidationFailure>();
        var projected = new Dictionary<(Guid, Guid, Guid?), decimal>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];

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
