using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

/// <summary>
/// <c>default_sale_location_id</c> e <c>default_receive_location_id</c> não são relacionamento no EF, que recusaria o
/// ciclo com <c>stock_locations.store_id</c> ao gravar os dois no mesmo commit. As FKs estão na migration AddStores,
/// <c>DEFERRABLE INITIALLY DEFERRED</c>: o banco confere no fim da transação (DOMAIN_LEVELS, par 1).
/// </summary>
public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.HasIndex(s => s.DefaultSaleLocationId);
        builder.HasIndex(s => s.DefaultReceiveLocationId);
    }
}

public class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).IsRequired().HasMaxLength(100);
        builder.HasOne(l => l.Store).WithMany(s => s.Locations).HasForeignKey(l => l.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserStoreConfiguration : IEntityTypeConfiguration<UserStore>
{
    public void Configure(EntityTypeBuilder<UserStore> builder)
    {
        builder.ToTable("user_stores");
        builder.HasKey(us => us.Id);
        builder.Property(us => us.Id).ValueGeneratedNever();
        builder.HasOne<User>().WithMany().HasForeignKey(us => us.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(us => us.Store).WithMany().HasForeignKey(us => us.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(us => new { us.UserId, us.StoreId }).IsUnique();
    }
}
