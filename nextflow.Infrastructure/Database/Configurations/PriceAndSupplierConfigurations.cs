using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("price_history", table =>
            table.HasCheckConstraint("ck_price_history_valid_range", "valid_to IS NULL OR valid_to >= valid_from"));
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Price).HasPrecision(12, 2);
        builder.Property(h => h.ChangedById).HasColumnName("changed_by");

        // Restrict: o banco recusa apagar variação com histórico, inclusive por cascata do produto (RN-CE58).
        builder.HasOne(h => h.Variant).WithMany(v => v.PriceHistory).HasForeignKey(h => h.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(h => h.ChangedBy).WithMany().HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);

        // No máximo uma faixa aberta por variação (RN-CE59). Na corrida de duas trocas, uma grava e a outra esbarra aqui.
        builder.HasIndex(h => h.VariantId).IsUnique().HasFilter("valid_to IS NULL").HasDatabaseName("ix_price_history_open");
    }
}

public class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> builder)
    {
        builder.ToTable("product_suppliers");
        builder.HasKey(ps => ps.Id);
        builder.Property(ps => ps.Id).ValueGeneratedNever();
        builder.Property(ps => ps.SupplierSku).HasMaxLength(50);
        builder.Property(ps => ps.MinOrderQty).HasPrecision(12, 3);
        builder.Property(ps => ps.OrderMultiple).HasPrecision(12, 3);

        // Um preferido só no validador: índice filtrado recusaria trocar o preferido entre dois vínculos no mesmo commit.
        builder.HasOne(ps => ps.Product).WithMany(p => p.Suppliers).HasForeignKey(ps => ps.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(ps => ps.Supplier).WithMany(s => s.Products).HasForeignKey(ps => ps.SupplierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ps => new { ps.ProductId, ps.SupplierId }).IsUnique();
    }
}

public class ProductEventConfiguration : IEntityTypeConfiguration<ProductEvent>
{
    public void Configure(EntityTypeBuilder<ProductEvent> builder)
    {
        builder.ToTable("product_events");
        builder.HasKey(pe => pe.Id);
        builder.Property(pe => pe.Id).ValueGeneratedNever();

        builder.HasOne(pe => pe.Product).WithMany(p => p.Events).HasForeignKey(pe => pe.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(pe => pe.Event).WithMany(e => e.Products).HasForeignKey(pe => pe.EventId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pe => new { pe.ProductId, pe.EventId }).IsUnique();
    }
}
