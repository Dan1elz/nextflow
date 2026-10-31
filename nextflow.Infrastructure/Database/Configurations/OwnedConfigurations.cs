using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

/// <summary>Referência polimórfica sem FK (DOMAIN_LEVELS): o CHECK limita o tipo e o use case valida a existência do dono.</summary>
public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses", t => t.HasCheckConstraint("ck_addresses_owner_type", "owner_type IN (1, 2)"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.ZipCode).IsRequired().HasMaxLength(8);
        builder.Property(a => a.Street).IsRequired().HasMaxLength(150);
        builder.Property(a => a.Number).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Complement).HasMaxLength(100);
        builder.Property(a => a.Neighborhood).IsRequired().HasMaxLength(100);
        builder.HasOne(a => a.City).WithMany().HasForeignKey(a => a.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.OwnerType, a.OwnerId });
    }
}

/// <summary>Referência polimórfica sem FK (DOMAIN_LEVELS): o CHECK limita o tipo e o use case valida a existência do dono.</summary>
public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts", t => t.HasCheckConstraint("ck_contacts_owner_type", "owner_type IN (1, 2)"));
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Value).IsRequired().HasMaxLength(150);
        builder.Ignore(c => c.IsPhone);
        builder.HasIndex(c => new { c.OwnerType, c.OwnerId });
    }
}
