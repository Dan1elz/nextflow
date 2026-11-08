using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<byte>();
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(n => n.Message).HasMaxLength(Notification.MessageMaxLength);
        builder.Property(n => n.Link).HasMaxLength(Notification.LinkMaxLength);
        builder.Property(n => n.DigestDate).HasColumnType("date");

        builder.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);

        // O sino lê as últimas do usuário e conta as não lidas.
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.UserId).HasFilter("read_at is null").HasDatabaseName("ix_notifications_unread");
        // Resumo diário (L-DF4): um por usuário, tipo e dia.
        builder.HasIndex(n => new { n.UserId, n.Type, n.DigestDate }).IsUnique().HasFilter("digest_date is not null");
    }
}
