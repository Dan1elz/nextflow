using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class ForecastSettingsConfiguration : IEntityTypeConfiguration<ForecastSettings>
{
    public void Configure(EntityTypeBuilder<ForecastSettings> builder)
    {
        builder.ToTable("forecast_settings", table =>
        {
            table.HasCheckConstraint("ck_forecast_settings_weekday", "weekday between 0 and 6");
            table.HasCheckConstraint("ck_forecast_settings_ai_batch_size", "ai_batch_size between 1 and 100");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Weekday).HasConversion<byte>();
        builder.Property(s => s.Time).HasColumnType("time");
        builder.Property(s => s.UpdatedById).HasColumnName("updated_by");

        builder.HasOne(s => s.UpdatedBy).WithMany().HasForeignKey(s => s.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
