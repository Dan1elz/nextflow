using Nextflow.Domain.Dtos.Products;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Interfaces.Domain.Models;

namespace Nextflow.Domain.Models;

/// <summary>
/// Agregado do catálogo: dono de atributos, valores e variações, sincronizados no mesmo commit. Tudo é variação: produto
/// sem grade tem uma variação padrão oculta (RN-CE09, RN-CE48). Não é <c>IUpdatable</c>: salvar pode trocar preço, e o
/// histórico exige quem alterou e a data (RN-CE25), que não vêm do DTO.
/// </summary>
public class Product : BaseModel, IDeletable
{
    public string Name { get; private set; } = string.Empty;
    public string? Brand { get; private set; }
    public string? Description { get; private set; }
    public ProductTypeEnum ProductType { get; private set; }
    public SaleUnitEnum SaleUnit { get; private set; }
    public short DecimalPlaces { get; private set; }
    public bool AllowsNegative { get; private set; }
    public bool HasVariants { get; private set; }
    public bool TracksLots { get; private set; }
    public int? ShelfLifeDays { get; private set; }
    public Guid? CollectionId { get; private set; }
    public LifecycleEnum Lifecycle { get; private set; } = LifecycleEnum.Active;
    public DateOnly? LaunchedOn { get; private set; }
    /// <summary>Mínimo de cada variação em todo local sem mínimo próprio (RN-CE33; substitui o fallback fixo da RN-CE06).</summary>
    public decimal DefaultMinQuantity { get; private set; }
    /// <summary>Exceção à política da classe (RN-CE29). Nulo = usa a cobertura alvo da classe ABC.</summary>
    public int? TargetCoverageDays { get; private set; }
    /// <summary>Calculada pela análise (F3, RN-CE29); nunca vem do formulário. Nula até a primeira análise.</summary>
    public AbcClassEnum? AbcClass { get; private set; }
    public string? ImageKey { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? UpdatedAt { get; private set; }

    public virtual Collection? Collection { get; private set; }
    public virtual ICollection<CategoryProduct> Categories { get; private set; } = [];
    public virtual ICollection<ProductAttribute> Attributes { get; private set; } = [];
    public virtual ICollection<ProductVariant> Variants { get; private set; } = [];
    public virtual ICollection<ProductEvent> Events { get; private set; } = [];
    public virtual ICollection<ProductSupplier> Suppliers { get; private set; } = [];

    public override string Preposition => "o";
    public override string Singular => "produto";
    public override string Plural => "produtos";

    private Product() { }

    public Product(ProductRequestDto dto, Guid changedById, DateOnly today) => Apply(dto, changedById, today);

    public void Update(ProductRequestDto dto, Guid changedById, DateOnly today)
    {
        Apply(dto, changedById, today);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Delete()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeLifecycle(LifecycleEnum lifecycle)
    {
        Lifecycle = lifecycle;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetImage(string key)
    {
        ImageKey = key;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveImage()
    {
        ImageKey = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Mínimo efetivo da variação no local: o do local, se houver; senão o padrão do produto (RN-CE33).</summary>
    public decimal EffectiveMinimum(ProductVariant variant, Guid locationId)
        => variant.LocationMinimums.FirstOrDefault(m => m.LocationId == locationId)?.MinQuantity ?? DefaultMinQuantity;

    /// <summary>Estoque baixo: disponível abaixo do mínimo efetivo (RN-CE33). Igual ao mínimo ainda não é baixo.</summary>
    public bool IsBelowMinimum(ProductVariant variant, Guid locationId, decimal available)
        => available < EffectiveMinimum(variant, locationId);

    private void Apply(ProductRequestDto dto, Guid changedById, DateOnly today)
    {
        Name = dto.Name.Trim();
        Brand = TrimToNull(dto.Brand);
        Description = TrimToNull(dto.Description);
        ProductType = dto.ProductType;
        SaleUnit = dto.SaleUnit;
        DecimalPlaces = dto.DecimalPlaces;
        AllowsNegative = dto.AllowsNegative;
        TracksLots = dto.TracksLots;
        // Sem controle de lote a vida útil não vale: como RN-CE41, campo fora da regra é gravado como nulo.
        ShelfLifeDays = dto.TracksLots ? dto.ShelfLifeDays : null;
        CollectionId = dto.CollectionId;
        LaunchedOn = dto.LaunchedOn;
        HasVariants = dto.Attributes.Count > 0;
        DefaultMinQuantity = dto.DefaultMinQuantity;
        TargetCoverageDays = dto.TargetCoverageDays;

        SyncCategories(dto.CategoryIds);
        var valueIds = SyncAttributes(dto.Attributes);
        var variants = SyncVariants(dto.Variants, valueIds, changedById, today);
        SyncMinimums(dto.LocationMinimums, variants);
        SyncEvents(dto.EventIds);
        SyncSuppliers(dto.Suppliers);
    }

    private void SyncCategories(List<Guid> categoryIds)
    {
        foreach (var link in Categories.Where(link => !categoryIds.Contains(link.CategoryId)).ToList())
            Categories.Remove(link);

        var linked = Categories.Select(link => link.CategoryId).ToHashSet();
        foreach (var categoryId in categoryIds.Distinct().Where(id => !linked.Contains(id)))
            Categories.Add(new CategoryProduct(categoryId, Id));
    }

    /// <summary>Devolve, para cada chave citável pelas variações (id ou <c>tempKey</c>), o id do valor (RN-CE50).</summary>
    private Dictionary<string, Guid> SyncAttributes(List<ProductAttributeRequestDto> dtos)
    {
        var kept = dtos.Select(dto => dto.Id).ToHashSet();
        foreach (var attribute in Attributes.Where(attribute => !kept.Contains(attribute.Id)).ToList())
            Attributes.Remove(attribute);

        var valueIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < dtos.Count; index++)
        {
            var dto = dtos[index];
            var attribute = Attributes.FirstOrDefault(a => a.Id == dto.Id);
            if (attribute is null)
            {
                attribute = new ProductAttribute(dto, Id, (short)index);
                Attributes.Add(attribute);
            }
            else
            {
                attribute.Update(dto, (short)index);
            }

            attribute.SyncValues(dto.Values, valueIds);
        }

        return valueIds;
    }

    /// <summary>
    /// Variação que saiu do formulário é preservada como removida, nunca apagada: ela já tem histórico de preço (RN-CE58).
    /// A F1.5 recusa a remoção antes de chegar aqui, na regra do use case, quando houver saldo ou movimento.
    /// </summary>
    /// <returns>As variações gravadas na ordem do formulário: a posição é a chave dos mínimos por local.</returns>
    private List<ProductVariant> SyncVariants(List<ProductVariantRequestDto> dtos, Dictionary<string, Guid> valueIds, Guid changedById, DateOnly today)
    {
        var ordered = new List<ProductVariant>(dtos.Count);
        var kept = dtos.Select(dto => dto.Id).ToHashSet();
        foreach (var variant in Variants.Where(variant => variant.RemovedOn == null && !kept.Contains(variant.Id)))
            variant.Remove(today);

        for (var index = 0; index < dtos.Count; index++)
        {
            var dto = dtos[index];
            var variant = Variants.FirstOrDefault(v => v.Id == dto.Id);
            if (variant is null)
            {
                variant = new ProductVariant(dto, Id, (short)index, changedById, today);
                Variants.Add(variant);
            }
            else
            {
                variant.Update(dto, (short)index, changedById, today);
            }

            variant.SyncValues(dto.AttributeValueIds.Select(key => valueIds[key]).ToList());
            ordered.Add(variant);
        }

        return ordered;
    }

    /// <summary>
    /// Cada mínimo cita a variação pela posição no formulário, que <see cref="SyncVariants"/> resolve para a variação gravada,
    /// nova ou não. Variação sem mínimo no formulário fica sem nenhum e usa o padrão do produto (RN-CE33).
    /// </summary>
    private static void SyncMinimums(List<ProductLocationMinimumDto> minimums, List<ProductVariant> variants)
    {
        for (var index = 0; index < variants.Count; index++)
        {
            var position = index;
            variants[index].SyncMinimums(minimums
                .Where(m => m.VariantIndex == position)
                .GroupBy(m => m.LocationId)
                .ToDictionary(g => g.Key, g => g.Last().MinQuantity));
        }
    }

    private void SyncEvents(List<Guid> eventIds)
    {
        foreach (var link in Events.Where(link => !eventIds.Contains(link.EventId)).ToList())
            Events.Remove(link);

        var linked = Events.Select(link => link.EventId).ToHashSet();
        foreach (var eventId in eventIds.Distinct().Where(id => !linked.Contains(id)))
            Events.Add(new ProductEvent(Id, eventId));
    }

    /// <summary>Quem saiu é removido; quem fica preserva o id, reconhecido pelo fornecedor, único no produto (RN-CE60).</summary>
    private void SyncSuppliers(List<ProductSupplierRequestDto> dtos)
    {
        var kept = dtos.Select(dto => dto.SupplierId).ToHashSet();
        foreach (var link in Suppliers.Where(link => !kept.Contains(link.SupplierId)).ToList())
            Suppliers.Remove(link);

        foreach (var dto in dtos)
        {
            var link = Suppliers.FirstOrDefault(s => s.SupplierId == dto.SupplierId);
            if (link is null)
                Suppliers.Add(new ProductSupplier(dto, Id));
            else
                link.Update(dto);
        }
    }

    private static string? TrimToNull(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
