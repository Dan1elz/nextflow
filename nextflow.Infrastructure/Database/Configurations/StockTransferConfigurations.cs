using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class StockTransferConfiguration : IEntityTypeConfiguration<StockTransfer>
{
    public void Configure(EntityTypeBuilder<StockTransfer> builder)
    {
        builder.ToTable("stock_transfers", table =>
            table.HasCheckConstraint("ck_stock_transfers_locations", "from_location_id <> to_location_id"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Number).UseIdentityByDefaultColumn();
        builder.HasIndex(t => t.Number).IsUnique();
        builder.Property(t => t.Note).HasMaxLength(500);
        builder.Property(t => t.CreatedById).HasColumnName("created_by");

        builder.HasOne(t => t.FromLocation).WithMany().HasForeignKey(t => t.FromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.ToLocation).WithMany().HasForeignKey(t => t.ToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ForecastRecommendation>().WithMany().HasForeignKey(t => t.ForecastRecommendationId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(t => t.ForecastRecommendationId);
    }
}

public class StockTransferItemConfiguration : IEntityTypeConfiguration<StockTransferItem>
{
    public void Configure(EntityTypeBuilder<StockTransferItem> builder)
    {
        builder.ToTable("stock_transfer_items", table =>
            table.HasCheckConstraint("ck_stock_transfer_items_quantity", "quantity > 0"));
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Quantity).HasPrecision(14, 3);
        builder.HasOne(i => i.Transfer).WithMany(t => t.Items).HasForeignKey(i => i.TransferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Variant).WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
