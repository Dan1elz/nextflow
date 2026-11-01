using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nextflow.Domain.Models;

namespace Nextflow.Infrastructure.Database.Configurations;

public class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        builder.ToTable("calendar_events", t =>
        {
            t.HasCheckConstraint("ck_calendar_events_scope", "scope IN (1, 2)");
            t.HasCheckConstraint("ck_calendar_events_date_rule", "date_rule IN (1, 2, 3, 4, 5)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

        builder.HasIndex(e => e.Name).IsUnique(); // RN-CE44
    }
}
