using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Brand).HasMaxLength(100);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.ImageKey).HasMaxLength(200);
        builder.Property(p => p.ProductType).HasConversion<byte>();
        builder.Property(p => p.SaleUnit).HasConversion<byte>();
        builder.Property(p => p.Lifecycle).HasConversion<byte>();
        builder.Property(p => p.AbcClass).HasConversion<byte?>();
        builder.Property(p => p.DefaultMinQuantity).HasPrecision(14, 3);

        builder.HasOne(p => p.Collection).WithMany(c => c.Products).HasForeignKey(p => p.CollectionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.Name);
    }
}

public class CategoryProductConfiguration : IEntityTypeConfiguration<CategoryProduct>
{
    public void Configure(EntityTypeBuilder<CategoryProduct> builder)
    {
        builder.ToTable("category_products");
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).ValueGeneratedNever();

        builder.HasOne(cp => cp.Category).WithMany(c => c.Products).HasForeignKey(cp => cp.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(cp => cp.Product).WithMany(p => p.Categories).HasForeignKey(cp => cp.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cp => new { cp.CategoryId, cp.ProductId }).IsUnique();
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> builder)
    {
        builder.ToTable("product_attributes");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Name).IsRequired().HasMaxLength(50);

        // Atributo que sai do formulário é apagado com os valores (RN-CE51). Nome único só no validator: índice único
        // recusaria trocar dois nomes de lugar no mesmo commit.
        builder.HasOne(a => a.Product).WithMany(p => p.Attributes).HasForeignKey(a => a.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> builder)
    {
        builder.ToTable("product_attribute_values");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Value).IsRequired().HasMaxLength(50);

        builder.HasOne(v => v.Attribute).WithMany(a => a.Values).HasForeignKey(v => v.AttributeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.Sku).IsRequired().HasMaxLength(50);
        builder.Property(v => v.Barcode).HasMaxLength(50);
        builder.Property(v => v.Price).HasPrecision(12, 2);
        builder.Property(v => v.AverageCost).HasPrecision(14, 4);
        builder.Property(v => v.LastCost).HasPrecision(14, 4);

        // Só a entrada com custo muda a versão: duas entradas simultâneas na mesma variação → DbUpdateConcurrencyException → 409.
        builder.Property(v => v.Version).IsConcurrencyToken();

        // RN-CE51: as tabelas da F1.5 apontam para cá com Restrict, e é isso que barra apagar variação com histórico.
        builder.HasOne(v => v.Product).WithMany(p => p.Variants).HasForeignKey(v => v.ProductId).OnDelete(DeleteBehavior.Cascade);

        // RN-CE57: a unicidade sem diferenciar maiúsculas está em lower(sku) e lower(barcode), em SQL na migration AddProducts,
        // porque o EF não modela índice por expressão. Estes dois ficam para o EF saber da unicidade e apagar a variação que
        // sai antes de gravar o mesmo código na que fica. Nulos não colidem: código de barras é opcional.
        builder.HasIndex(v => v.Sku).IsUnique();
        builder.HasIndex(v => v.Barcode).IsUnique();
    }
}

public class ProductVariantValueConfiguration : IEntityTypeConfiguration<ProductVariantValue>
{
    public void Configure(EntityTypeBuilder<ProductVariantValue> builder)
    {
        builder.ToTable("product_variant_values");
        builder.HasKey(vv => vv.Id);
        builder.Property(vv => vv.Id).ValueGeneratedNever();

        builder.HasOne(vv => vv.Variant).WithMany(v => v.AttributeValues).HasForeignKey(vv => vv.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(vv => vv.AttributeValue).WithMany().HasForeignKey(vv => vv.AttributeValueId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vv => new { vv.VariantId, vv.AttributeValueId }).IsUnique();
    }
}
