using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration
    : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(document => document.OwnerUserId)
            .HasColumnName("owner_user_id")
            .IsRequired(false);

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(document => document.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(document => document.OwnerUserId)
            .HasDatabaseName("IX_documents_owner_user_id");

        builder.Property(document => document.OwnerTeamId)
            .HasColumnName("owner_team_id")
            .IsRequired(false);

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(document => document.OwnerTeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(document => document.OwnerTeamId)
            .HasDatabaseName("IX_documents_owner_team_id");

        builder.Property(document => document.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(document => document.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(document => document.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasIndex(document => new
            {
                document.CreatedAtUtc,
                document.Id
            })
            .IsDescending(true, false)
            .HasDatabaseName("IX_documents_created_at_utc_id");
    }
}
