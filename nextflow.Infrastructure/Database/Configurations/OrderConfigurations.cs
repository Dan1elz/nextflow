using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public const string NumberSequence = "order_number_seq";

    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", table =>
        {
            table.HasCheckConstraint("ck_orders_totals", "subtotal >= 0 AND discount_total >= 0 AND total >= 0");
            table.HasCheckConstraint("ck_orders_change", "change IS NULL OR change >= 0");
        });
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Number).HasDefaultValueSql($"nextval('{NumberSequence}')").ValueGeneratedOnAdd();
        builder.HasIndex(o => o.Number).IsUnique();
        builder.Property(o => o.Status).HasConversion<byte>();
        builder.Property(o => o.Channel).HasConversion<byte>();
        builder.Property(o => o.Subtotal).HasPrecision(12, 2);
        builder.Property(o => o.DiscountTotal).HasPrecision(12, 2);
        builder.Property(o => o.Total).HasPrecision(12, 2);
        builder.Property(o => o.CashReceived).HasPrecision(12, 2);
        builder.Property(o => o.Change).HasPrecision(12, 2);

        builder.HasOne(o => o.Store).WithMany().HasForeignKey(o => o.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.SaleLocation).WithMany().HasForeignKey(o => o.SaleLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Client).WithMany().HasForeignKey(o => o.ClientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(o => o.Payments).WithOne(p => p.Order).HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);

        // Listagem de pedidos (F2.5) e a base de vendas da previsão filtram por loja, status e data.
        builder.HasIndex(o => new { o.StoreId, o.Status, o.CreatedAt });
        builder.HasIndex(o => o.ClientId);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", table =>
        {
            table.HasCheckConstraint("ck_order_items_quantity", "quantity > 0");
            table.HasCheckConstraint("ck_order_items_discount", "discount >= 0 AND line_total >= 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Quantity).HasPrecision(14, 3);
        builder.Property(i => i.ListPrice).HasPrecision(12, 2);
        builder.Property(i => i.UnitPrice).HasPrecision(12, 2);
        builder.Property(i => i.Discount).HasPrecision(12, 2);
        builder.Property(i => i.LineTotal).HasPrecision(12, 2);
        builder.Property(i => i.UnitCost).HasPrecision(14, 4);
        builder.Ignore(i => i.GrossTotal);

        builder.HasOne(i => i.Variant).WithMany().HasForeignKey(i => i.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Promotion).WithMany().HasForeignKey(i => i.PromotionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.OrderId, i.Position }).IsUnique();
        builder.HasIndex(i => i.VariantId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table => table.HasCheckConstraint("ck_payments_amount", "amount > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Method).HasConversion<byte>();
        builder.Property(p => p.Amount).HasPrecision(12, 2);
    }
}
