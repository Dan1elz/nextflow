using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("purchase_orders", table =>
            table.HasCheckConstraint("ck_purchase_orders_total", "total >= 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Número visível sequencial (L-CP8): identidade do Postgres, sem buraco de concorrência no código.
        builder.Property(p => p.Number).UseIdentityByDefaultColumn();
        builder.HasIndex(p => p.Number).IsUnique();

        builder.Property(p => p.Status).HasConversion<byte>();
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.Property(p => p.CancelReason).HasMaxLength(500);
        builder.Property(p => p.Total).HasPrecision(14, 2);
        builder.Property(p => p.CreatedById).HasColumnName("created_by");
        builder.Property(p => p.ReceivedById).HasColumnName("received_by");

        // Concorrência otimista pelo xmin: o update leva a versão lida e 409 se outra gravação passou no meio.
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne(p => p.Supplier).WithMany().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Store).WithMany().HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ReceiveLocation).WithMany().HasForeignKey(p => p.ReceiveLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ReceivedBy).WithMany().HasForeignKey(p => p.ReceivedById).OnDelete(DeleteBehavior.Restrict);

        // Listagem por loja e status (T-CP1) e lead time real por fornecedor (F2.8, RN-CP06).
        builder.HasIndex(p => new { p.StoreId, p.Status });
        builder.HasIndex(p => new { p.SupplierId, p.Status });
    }
}

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items", table =>
        {
            table.HasCheckConstraint("ck_purchase_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_purchase_items_unit_cost", "unit_cost >= 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Quantity).HasPrecision(14, 3);
        builder.Property(i => i.UnitCost).HasPrecision(14, 4);
        builder.Property(i => i.Subtotal).HasPrecision(14, 2);
        builder.Property(i => i.AverageCostAfter).HasPrecision(14, 4);

        // Sai com o pedido (sincronização do formulário); a variação nunca é apagada (RN-CE58).
        builder.HasOne(i => i.PurchaseOrder).WithMany(p => p.Items).HasForeignKey(i => i.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.Variant).WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.Restrict);

        // Uma linha por variação no pedido (T-CP2).
        builder.HasIndex(i => new { i.PurchaseOrderId, i.VariantId }).IsUnique();
        builder.HasIndex(i => i.VariantId);
    }
}
