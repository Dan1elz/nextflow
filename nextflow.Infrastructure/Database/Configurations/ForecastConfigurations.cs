using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class ForecastRunConfiguration : IEntityTypeConfiguration<ForecastRun>
{
    public void Configure(EntityTypeBuilder<ForecastRun> builder)
    {
        builder.ToTable("forecast_runs", table =>
        {
            // Zero = ainda sem horizonte (Running); depois de 1 a 6 meses (RN-DF17).
            table.HasCheckConstraint("ck_forecast_runs_horizon", "horizon_months between 0 and 6");
            table.HasCheckConstraint("ck_forecast_runs_batches", "batches_completed + batches_failed <= batches_total");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Trigger).HasConversion<byte>();
        builder.Property(r => r.Status).HasConversion<byte>();
        builder.Property(r => r.ReferenceDate).HasColumnType("date");
        builder.Property(r => r.AiProvider).HasMaxLength(40);
        builder.Property(r => r.AiModel).HasMaxLength(80);
        builder.Property(r => r.Summary).HasColumnType("text");
        builder.Property(r => r.ErrorMessage).HasColumnType("text");

        builder.Property(r => r.RequestedById).HasColumnName("requested_by");

        builder.HasOne(r => r.RequestedBy).WithMany().HasForeignKey(r => r.RequestedById).OnDelete(DeleteBehavior.Restrict);

        // Só uma análise em andamento por vez (RN-DF23): o índice parcial garante, a F3.3 devolve 409 antes de chegar aqui.
        builder.HasIndex(r => r.Status).IsUnique().HasFilter("status = 1").HasDatabaseName("ux_forecast_runs_running");
        builder.HasIndex(r => r.StartedAt);
    }
}

/// <summary>Parte de cada loja na compra recomendada (RN-DF27).</summary>
public class ForecastRecommendationStoreConfiguration : IEntityTypeConfiguration<ForecastRecommendationStore>
{
    public void Configure(EntityTypeBuilder<ForecastRecommendationStore> builder)
    {
        builder.ToTable("forecast_recommendation_stores");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.SharePercent).HasPrecision(6, 2);
        builder.Property(s => s.Shortfall).HasPrecision(14, 3);
        builder.Property(s => s.Quantity).HasPrecision(14, 3);
        builder.HasOne(s => s.Recommendation).WithMany(r => r.Stores).HasForeignKey(s => s.RecommendationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.Store).WithMany().HasForeignKey(s => s.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.RecommendationId, s.StoreId }).IsUnique();
    }
}

/// <summary>Pedidos gerados por uma recomendação "Comprar" (RN-DF28).</summary>
public class ForecastRecommendationPurchaseOrderConfiguration : IEntityTypeConfiguration<ForecastRecommendationPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<ForecastRecommendationPurchaseOrder> builder)
    {
        builder.ToTable("forecast_recommendation_purchase_orders");
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).ValueGeneratedNever();
        builder.HasOne(link => link.Recommendation).WithMany(r => r.PurchaseOrders).HasForeignKey(link => link.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(link => link.PurchaseOrder).WithMany().HasForeignKey(link => link.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.RecommendationId, link.PurchaseOrderId }).IsUnique();
        builder.HasIndex(link => link.PurchaseOrderId);
    }
}

public class ForecastRecommendationConfiguration : IEntityTypeConfiguration<ForecastRecommendation>
{
    public void Configure(EntityTypeBuilder<ForecastRecommendation> builder)
    {
        builder.ToTable("forecast_recommendations", table =>
        {
            table.HasCheckConstraint("ck_forecast_recommendations_quantity", "suggested_quantity is null or suggested_quantity >= 0");
            table.HasCheckConstraint("ck_forecast_recommendations_discount", "suggested_discount_percent is null or suggested_discount_percent between 0 and 90");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Action).HasConversion<byte>();
        builder.Property(r => r.Priority).HasConversion<byte>();
        builder.Property(r => r.Confidence).HasConversion<byte>();
        builder.Property(r => r.Status).HasConversion<byte>();
        builder.Property(r => r.LeadTimeSource).HasConversion<byte>();
        builder.Property(r => r.Deadline).HasColumnType("date");
        builder.Property(r => r.Reason).HasMaxLength(1000);
        builder.Property(r => r.ReasonCalculated).HasMaxLength(1000);
        builder.Property(r => r.DismissReason).HasMaxLength(255);
        builder.Property(r => r.DismissReasonCode).HasConversion<byte?>();
        builder.Property(r => r.AppliedById).HasColumnName("applied_by");
        builder.HasOne<StockTransfer>().WithMany().HasForeignKey(r => r.AppliedTransferId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(r => r.TransferFromLocation).WithMany().HasForeignKey(r => r.TransferFromLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.TransferToLocation).WithMany().HasForeignKey(r => r.TransferToLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(r => r.SuggestedQuantity).HasPrecision(14, 3);
        builder.Property(r => r.CalculatedQuantity).HasPrecision(14, 3);
        builder.Property(r => r.SuggestedDiscountPercent).HasPrecision(5, 2);
        builder.Property(r => r.CurrentStock).HasPrecision(14, 3);
        builder.Property(r => r.IncomingStock).HasPrecision(14, 3);
        builder.Property(r => r.ProjectedDemand).HasPrecision(14, 3);
        builder.Property(r => r.NextMonthDemand).HasPrecision(14, 3);
        builder.Property(r => r.CoverageDays).HasPrecision(8, 1);
        builder.Property(r => r.NextMonthSeasonalIndex).HasPrecision(8, 4);
        builder.Property(r => r.SafetyStock).HasPrecision(14, 3);

        builder.HasOne(r => r.ForecastRun).WithMany(run => run.Recommendations).HasForeignKey(r => r.ForecastRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Product).WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AppliedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.AppliedPurchaseOrder).WithMany().HasForeignKey(r => r.AppliedPurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.AppliedPromotion).WithMany().HasForeignKey(r => r.AppliedPromotionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ForecastRunId);
        builder.HasIndex(r => new { r.ProductId, r.Status });
        // Uma recomendação por produto em cada análise (RN-DF21).
        builder.HasIndex(r => new { r.ForecastRunId, r.ProductId }).IsUnique();
        // A nova análise expira as pendentes da anterior (RN-DF14) e o painel lê as pendentes.
        builder.HasIndex(r => r.Status).HasFilter("status = 1").HasDatabaseName("ix_forecast_recommendations_pending");
    }
}

public class ForecastRecommendationItemConfiguration : IEntityTypeConfiguration<ForecastRecommendationItem>
{
    public void Configure(EntityTypeBuilder<ForecastRecommendationItem> builder)
    {
        builder.ToTable("forecast_recommendation_items", table =>
        {
            table.HasCheckConstraint("ck_forecast_recommendation_items_quantity", "quantity >= 0");
            table.HasCheckConstraint("ck_forecast_recommendation_items_share", "share_percent between 0 and 100");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Quantity).HasPrecision(14, 3);
        builder.Property(i => i.SharePercent).HasPrecision(7, 4);

        builder.HasOne(i => i.Recommendation).WithMany(r => r.Items).HasForeignKey(i => i.RecommendationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Variant).WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.RecommendationId, i.VariantId }).IsUnique();
    }
}

public class ForecastProjectionConfiguration : IEntityTypeConfiguration<ForecastProjection>
{
    public void Configure(EntityTypeBuilder<ForecastProjection> builder)
    {
        builder.ToTable("forecast_projections", table =>
        {
            table.HasCheckConstraint("ck_forecast_projections_month", "month between 1 and 12");
            table.HasCheckConstraint("ck_forecast_projections_quantity", "projected_quantity >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.ProjectedQuantity).HasPrecision(14, 3);

        builder.HasOne(p => p.ForecastRun).WithMany(r => r.Projections).HasForeignKey(p => p.ForecastRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ForecastRunId, p.ProductId, p.Year, p.Month }).IsUnique();
        // A precisão (RN-DF19) procura a projeção de um produto num mês, em qualquer análise.
        builder.HasIndex(p => new { p.ProductId, p.Year, p.Month });
    }
}

public class ForecastAiBatchConfiguration : IEntityTypeConfiguration<ForecastAiBatch>
{
    public void Configure(EntityTypeBuilder<ForecastAiBatch> builder)
    {
        builder.ToTable("forecast_ai_batches", table =>
        {
            table.HasCheckConstraint("ck_forecast_ai_batches_number", "batch_number >= 1");
        });
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Status).HasConversion<byte>();
        builder.Property(b => b.ErrorMessage).HasMaxLength(500);

        builder.HasOne(b => b.ForecastRun).WithMany(r => r.Batches).HasForeignKey(b => b.ForecastRunId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ForecastRunId, b.BatchNumber }).IsUnique();
    }
}
