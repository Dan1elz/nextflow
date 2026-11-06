using Nextflow.Domain.Utils;
using Nextflow.Domain.ValueObjects;
using Nextflow.Infrastructure.Seeders.Demo;

namespace Nextflow.Tests.Unit.Stock;

/// <summary>
/// O catálogo do seed (F1.11) entra direto pelos modelos, sem passar pela API: estes testes garantem que ele respeitaria
/// as mesmas regras do formulário (SKU único, preço e custo positivos, CNPJ válido, grade coerente).
/// </summary>
public class DemoCatalogTests
{
    [Fact]
    public void Should_HaveAboutFortyProductsWithAndWithoutGrid_When_CatalogIsBuilt()
    {
        DemoCatalog.Products.Length.ShouldBeInRange(35, 50);
        DemoCatalog.Products.ShouldContain(p => p.Grid != null);
        DemoCatalog.Products.ShouldContain(p => p.Grid == null);
        DemoCatalog.Products.Select(p => p.Name).ShouldBeUnique();
    }

    [Fact]
    public void Should_HaveUniqueSkusAndPositivePrices_When_CatalogIsBuilt()
    {
        var variants = DemoCatalog.Products.SelectMany(DemoCatalog.VariantsOf).ToList();
        variants.Select(v => v.Sku).ShouldBeUnique();
        variants.ShouldAllBe(v => v.Price > 0 && v.Cost > 0 && v.Cost < v.Price);
    }

    [Fact]
    public void Should_UseValidCnpjAndKnownSuppliers_When_CatalogIsBuilt()
    {
        DemoCatalog.Suppliers.ShouldAllBe(s => TaxDocument.IsValidCnpj(s.Cnpj) && s.LeadTimeDays > 0);
        DemoCatalog.Products.ShouldAllBe(p => p.SupplierIndex >= 0 && p.SupplierIndex < DemoCatalog.Suppliers.Length);
    }

    [Fact]
    public void Should_LinkOnlyNationalEvents_When_CatalogIsBuilt()
    {
        string[] national =
        [
            CommercialCalendar.EasterName, CommercialCalendar.MothersDayName, CommercialCalendar.ValentinesDayName,
            CommercialCalendar.ChristmasName,
        ];
        DemoCatalog.Products.SelectMany(p => p.Events).Distinct().ShouldBe(national, ignoreOrder: true);
    }
}
