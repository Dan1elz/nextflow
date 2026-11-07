using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class ProductSalesMonthlyConfiguration : IEntityTypeConfiguration<ProductSalesMonthly>
{
    public void Configure(EntityTypeBuilder<ProductSalesMonthly> builder)
    {
        builder.ToTable("product_sales_monthly", table =>
        {
            table.HasCheckConstraint("ck_product_sales_monthly_month", "month between 1 and 12");
            table.HasCheckConstraint("ck_product_sales_monthly_quantities",
                "quantity_sold >= 0 and quantity_sold_on_promo >= 0 and quantity_sold_on_promo <= quantity_sold and lost_demand_qty >= 0");
            table.HasCheckConstraint("ck_product_sales_monthly_days_out_of_stock", "days_out_of_stock between 0 and 31");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.QuantitySold).HasPrecision(14, 3);
        builder.Property(p => p.QuantitySoldOnPromo).HasPrecision(14, 3);
        builder.Property(p => p.LostDemandQty).HasPrecision(14, 3);
        builder.Property(p => p.AvgPrice).HasPrecision(14, 2);
        builder.Property(p => p.Revenue).HasPrecision(14, 2);

        // A chave de negócio é produto × ano × mês; o id técnico segue o padrão das outras tabelas derivadas (F1.8).
        builder.HasIndex(p => new { p.ProductId, p.Year, p.Month }).IsUnique();
        builder.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StockDailySnapshotConfiguration : IEntityTypeConfiguration<StockDailySnapshot>
{
    public void Configure(EntityTypeBuilder<StockDailySnapshot> builder)
    {
        builder.ToTable("stock_daily_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.ClosingQuantity).HasPrecision(14, 3);
        builder.Property(s => s.Day).HasColumnType("date");

        builder.HasIndex(s => new { s.VariantId, s.LocationId, s.Day }).IsUnique();
        builder.HasOne(s => s.Variant).WithMany().HasForeignKey(s => s.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Location).WithMany().HasForeignKey(s => s.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ProductSeasonalityConfiguration : IEntityTypeConfiguration<ProductSeasonality>
{
    public void Configure(EntityTypeBuilder<ProductSeasonality> builder)
    {
        builder.ToTable("product_seasonality", table =>
        {
            table.HasCheckConstraint("ck_product_seasonality_month", "month between 1 and 12");
            // Limite do índice (RN-DF03) e janela de histórico (RN-DF03: só com 12 meses ou mais, até 24).
            table.HasCheckConstraint("ck_product_seasonality_index", "seasonal_index between 0.05 and 5");
            table.HasCheckConstraint("ck_product_seasonality_history", "months_of_history between 12 and 24");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.SeasonalIndex).HasPrecision(8, 4);

        builder.HasIndex(p => new { p.ProductId, p.Month }).IsUnique();
        builder.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
