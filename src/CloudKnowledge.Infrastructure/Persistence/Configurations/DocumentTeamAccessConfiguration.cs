using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class DocumentTeamAccessConfiguration
    : IEntityTypeConfiguration<DocumentTeamAccess>
{
    public void Configure(
        EntityTypeBuilder<DocumentTeamAccess> builder)
    {
        builder.ToTable("document_team_access");

        builder.HasKey(access => new
        {
            access.DocumentId,
            access.TeamId
        });

        builder.Property(access => access.DocumentId)
            .HasColumnName("document_id");

        builder.Property(access => access.TeamId)
            .HasColumnName("team_id");

        builder.Property(access => access.SharedAtUtc)
            .HasColumnName("shared_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(access => access.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(access => access.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(access => access.TeamId)
            .HasDatabaseName("IX_document_team_access_team_id");
    }
}