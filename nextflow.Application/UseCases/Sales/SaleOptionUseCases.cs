using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos;
using Nextflow.Domain.Dtos.Sales;
using Nextflow.Domain.Interfaces.Application.Services;
using Nextflow.Domain.Interfaces.Application.UseCases;
using Nextflow.Domain.Interfaces.Infrastructure.Repositories;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;

namespace Nextflow.Application.UseCases.Sales;

/// <summary>
/// Seletor de venda (T-VD1). A loja escolhe o local: o informado (ativo, vendável e da mesma loja) ou o de venda padrão.
/// Entram as variações não removidas de produtos ativos, inclusive inativas e de produto fora de linha (RN-CE19 só bloqueia
/// compra). Código de barras ou SKU exatos vêm primeiro. Preço efetivo só pelo <see cref="IProductPricingService"/>
/// (RN-VD02); disponível = saldo físico no local, enquanto não houver orçamento (RN-CE08).
/// </summary>
public class GetVariantSaleOptionsUseCase(
    IProductVariantRepository variantRepository,
    IStoreRepository storeRepository,
    IStockLocationRepository locationRepository,
    IStockBalanceRepository balanceRepository,
    IFileStorage fileStorage,
    IProductPricingService pricing,
    IResultFactory<List<VariantSaleOptionDto>> resultFactory) : IGetVariantSaleOptionsUseCase
{
    private const int MaxLimit = 50;
    private const int DefaultLimit = 20;

    public async Task<ResultDto<List<VariantSaleOptionDto>>> Execute(
        string? search, Guid storeId, Guid? locationId, int limit, CancellationToken ct = default)
    {
        var store = await storeRepository.GetById(storeId, ct: ct);
        if (store is null || !store.IsActive)
            return resultFactory.FieldFailure("storeId", "Loja não encontrada ou inativa.");

        var saleLocationId = locationId ?? store.DefaultSaleLocationId;
        if (saleLocationId is not { } resolvedLocationId)
            return resultFactory.FieldFailure("locationId", "A loja não tem local de venda padrão.");

        var location = await locationRepository.GetById(resolvedLocationId, ct: ct);
        if (location is null || !location.IsActive || !location.IsSellable || location.StoreId != storeId)
            return resultFactory.FieldFailure("locationId", "O local precisa ser um local de venda ativo da loja.");

        var term = search?.Trim().ToLowerInvariant() ?? string.Empty;
        var variants = (await variantRepository.GetAll(
            v => v.RemovedOn == null && v.Product.IsActive && (term == string.Empty
                || v.Product.Name.ToLower().Contains(term)
                || v.Sku.ToLower().Contains(term)
                || (v.Barcode != null && v.Barcode.ToLower().Contains(term))),
            0,
            limit is >= 1 and <= MaxLimit ? limit : DefaultLimit,
            GetInclude(),
            q => q.OrderBy(v => (v.Barcode != null && v.Barcode.ToLower() == term) || v.Sku.ToLower() == term ? 0 : 1)
                .ThenBy(v => v.Product.Name).ThenBy(v => v.SortOrder).ThenBy(v => v.Id),
            ct)).ToList();

        var variantIds = variants.Select(v => v.Id).ToList();
        var available = (await balanceRepository.GetAll(
                b => variantIds.Contains(b.VariantId) && b.LocationId == resolvedLocationId, 0, int.MaxValue, ct: ct))
            .GroupBy(b => b.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.Quantity));

        return resultFactory.Success([.. variants.Select(v => new VariantSaleOptionDto(
            v, fileStorage.GetPublicUrl(v.Product.ImageKey), pricing, resolvedLocationId, available.GetValueOrDefault(v.Id)))]);
    }

    private static Func<IQueryable<ProductVariant>, IQueryable<ProductVariant>> GetInclude()
        => q => q.Include(v => v.Product)
            .Include(v => v.AttributeValues).ThenInclude(link => link.AttributeValue).ThenInclude(value => value.Attribute);
}
