using CloudKnowledge.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration
    : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(
        EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(chunk => chunk.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(chunk => chunk.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.Property(chunk => chunk.Content)
            .HasColumnName("content")
            .HasColumnType("text")
            .IsRequired();

        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "to_tsvector('simple'::regconfig, coalesce(content, ''::text))",
                stored: true);

        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                chunk => new
                {
                    chunk.DocumentId,
                    chunk.Position
                })
            .IsUnique()
            .HasDatabaseName(
                "IX_document_chunks_document_id_position");

        builder.HasIndex("SearchVector")
            .HasDatabaseName(
                "IX_document_chunks_search_vector")
            .HasMethod("GIN");
    }
}
