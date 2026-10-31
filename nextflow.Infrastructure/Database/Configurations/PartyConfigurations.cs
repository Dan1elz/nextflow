using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Database.Converters;

namespace Nextflow.Infrastructure.Database.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).HasMaxLength(100);
        builder.Property(c => c.Cpf!).HasConversion(ValueObjectConverters.TaxDocument).HasMaxLength(11);
        builder.Ignore(c => c.FullName);

        // RN-CD01, L-CP6: único quando informado (o índice único do Postgres aceita vários NULL).
        builder.HasIndex(c => c.Cpf).IsUnique();
    }
}

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers", t => t.HasCheckConstraint("ck_suppliers_lead_time_days", "lead_time_days BETWEEN 1 AND 180"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).IsRequired().HasMaxLength(150);
        builder.Property(s => s.Cnpj).HasConversion(ValueObjectConverters.TaxDocument).IsRequired().HasMaxLength(14);
        builder.Property(s => s.Note).HasMaxLength(500);

        builder.HasIndex(s => s.Cnpj).IsUnique(); // RN-CD02
    }
}
