using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotions", table =>
        {
            table.HasCheckConstraint("ck_promotions_discount", "discount_percent >= 1 AND discount_percent <= 90");
            table.HasCheckConstraint("ck_promotions_period", "end_date >= start_date");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.DiscountPercent).HasPrecision(5, 2);
        builder.Property(p => p.Reason).HasMaxLength(200);
        builder.Property(p => p.Source).HasConversion<byte>();
        builder.Property(p => p.CreatedById).HasColumnName("created_by");

        builder.HasOne(p => p.Product).WithMany(product => product.Promotions).HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Variant).WithMany().HasForeignKey(p => p.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);

        // Sobreposição (RN-VD11) e preço efetivo procuram as ativas do produto pelo período.
        builder.HasIndex(p => new { p.ProductId, p.StartDate, p.EndDate }).HasFilter("is_active");
        builder.HasIndex(p => p.StartDate);
    }
}
