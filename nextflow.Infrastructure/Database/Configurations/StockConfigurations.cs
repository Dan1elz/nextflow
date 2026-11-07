using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class StockBalanceConfiguration : IEntityTypeConfiguration<StockBalance>
{
    public void Configure(EntityTypeBuilder<StockBalance> builder)
    {
        builder.ToTable("stock_balances");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Quantity).HasPrecision(14, 3);

        // Concorrência otimista (RN-CE14): o UPDATE leva a versão lida no WHERE; se outra gravação passou no meio, nada é
        // atualizado, o EF lança DbUpdateConcurrencyException e o middleware devolve 409.
        builder.Property(b => b.Version).IsConcurrencyToken();

        builder.HasOne(b => b.Variant).WithMany(v => v.Balances).HasForeignKey(b => b.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Location).WithMany(l => l.Balances).HasForeignKey(b => b.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.Lot).WithMany().HasForeignKey(b => b.LotId).OnDelete(DeleteBehavior.Restrict);

        // Um saldo por variação × local × lote, com lote nulo contando como valor (produto sem lote, RN-CE09). Duas
        // primeiras entradas simultâneas na mesma chave: uma grava e a outra esbarra aqui (409 no middleware).
        builder.HasIndex(b => new { b.VariantId, b.LocationId, b.LotId }).IsUnique().AreNullsDistinct(false);
        builder.HasIndex(b => b.LocationId);
    }
}

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", table =>
        {
            // A quantidade leva o sinal do sentido: entrada positiva, saída negativa, nunca zero (RN-CE01).
            table.HasCheckConstraint("ck_stock_movements_signed_quantity", "(type = 1 AND quantity > 0) OR (type = 2 AND quantity < 0)");
            table.HasCheckConstraint("ck_stock_movements_unit_cost", "unit_cost IS NULL OR unit_cost >= 0");
        });
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasConversion<byte>();
        builder.Property(m => m.Reason).HasConversion<byte>();
        builder.Property(m => m.SourceType).HasConversion<byte>();
        builder.Property(m => m.Quantity).HasPrecision(14, 3);
        builder.Property(m => m.BalanceAfter).HasPrecision(14, 3);
        builder.Property(m => m.UnitCost).HasPrecision(14, 4);
        builder.Property(m => m.Note).HasMaxLength(500);

        builder.HasOne(m => m.Variant).WithMany().HasForeignKey(m => m.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Location).WithMany().HasForeignKey(m => m.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Lot).WithMany().HasForeignKey(m => m.LotId).OnDelete(DeleteBehavior.Restrict);

        // Kardex (F1.8): por variação e local na ordem do tempo, e por documento de origem.
        builder.HasIndex(m => new { m.VariantId, m.LocationId, m.CreatedAt });
        builder.HasIndex(m => new { m.SourceType, m.SourceId });
    }
}

public class LotConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.ToTable("lots");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Code).HasMaxLength(Lot.CodeMaxLength).IsRequired();

        builder.HasOne(l => l.Variant).WithMany().HasForeignKey(l => l.VariantId).OnDelete(DeleteBehavior.Restrict);

        // O código é único por variação (RN-CE10): a mesma chegada em dois locais é o mesmo lote. Dois recebimentos
        // simultâneos com o mesmo código novo: um grava e o outro esbarra aqui (409 no middleware).
        builder.HasIndex(l => new { l.VariantId, l.Code }).IsUnique();
        builder.HasIndex(l => l.ExpiresOn);
    }
}

public class StockLocationMinimumConfiguration : IEntityTypeConfiguration<StockLocationMinimum>
{
    public void Configure(EntityTypeBuilder<StockLocationMinimum> builder)
    {
        builder.ToTable("stock_location_minimums", table =>
            table.HasCheckConstraint("ck_stock_location_minimums_min_quantity", "min_quantity >= 0"));
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.MinQuantity).HasPrecision(14, 3);

        // Sai com a variação (sincronização do formulário, RN-CE51); o local nunca é apagado, só inativado.
        builder.HasOne(m => m.Variant).WithMany(v => v.LocationMinimums).HasForeignKey(m => m.VariantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(m => m.Location).WithMany().HasForeignKey(m => m.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Um mínimo por variação × local (RN-CE33); id técnico com índice único, como as tabelas de vínculo (F1.3).
        builder.HasIndex(m => new { m.VariantId, m.LocationId }).IsUnique();
        builder.HasIndex(m => m.LocationId);
    }
}

public class StockPolicyConfiguration : IEntityTypeConfiguration<StockPolicy>
{
    public void Configure(EntityTypeBuilder<StockPolicy> builder)
    {
        builder.ToTable("stock_policies", table =>
        {
            table.HasCheckConstraint("ck_stock_policies_target_coverage_days", "target_coverage_days >= 0");
            table.HasCheckConstraint("ck_stock_policies_safety_days", "safety_days >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.AbcClass).HasConversion<byte>();

        // Uma linha por classe (RN-CE29): a classe é a chave de negócio.
        builder.HasIndex(p => p.AbcClass).IsUnique();
    }
}
