using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.AcronymIso).IsRequired().HasMaxLength(2);
        builder.Property(c => c.BacenCode).HasMaxLength(5);
        builder.HasIndex(c => c.AcronymIso).IsUnique();
    }
}

public class StateConfiguration : IEntityTypeConfiguration<State>
{
    public void Configure(EntityTypeBuilder<State> builder)
    {
        builder.ToTable("states");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Acronym).IsRequired().HasMaxLength(2);
        builder.Property(s => s.IbgeCode).IsRequired().HasMaxLength(2);
        builder.HasIndex(s => s.IbgeCode).IsUnique();
        builder.HasOne(s => s.Country).WithMany(c => c.States).HasForeignKey(s => s.CountryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.IbgeCode).IsRequired().HasMaxLength(7);
        builder.HasIndex(c => c.IbgeCode).IsUnique();
        builder.HasOne(c => c.State).WithMany(s => s.Cities).HasForeignKey(c => c.StateId).OnDelete(DeleteBehavior.Restrict);
    }
}
