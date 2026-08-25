using CloudKnowledge.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class TeamConfiguration
    : IEntityTypeConfiguration<Team>
{
    public void Configure(
        EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasKey(team => team.Id);

        builder.Property(team => team.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(team => team.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(team => team.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}