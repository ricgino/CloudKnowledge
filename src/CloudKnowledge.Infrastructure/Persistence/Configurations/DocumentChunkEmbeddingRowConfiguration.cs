using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudKnowledge.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkEmbeddingRowConfiguration
    : IEntityTypeConfiguration<DocumentChunkEmbeddingRow>
{
    public void Configure(
        EntityTypeBuilder<DocumentChunkEmbeddingRow> builder)
    {
        builder.ToTable(
            "document_chunk_embeddings");

        builder.HasKey(
            embedding =>
                embedding.ChunkId);

        builder.Property(
                embedding =>
                    embedding.ChunkId)
            .HasColumnName("chunk_id")
            .ValueGeneratedNever();

        builder.Property(
                embedding =>
                    embedding.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(
                embedding =>
                    embedding.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)")
            .IsRequired();

        builder.HasOne<DocumentChunk>()
            .WithOne()
            .HasForeignKey<DocumentChunkEmbeddingRow>(
                embedding =>
                    embedding.ChunkId)
            .OnDelete(
                DeleteBehavior.Cascade);

        builder.HasIndex(
                embedding =>
                    embedding.DocumentId)
            .HasDatabaseName(
                "IX_document_chunk_embeddings_document_id");
    }
}