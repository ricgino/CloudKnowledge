using CloudKnowledge.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using CloudKnowledge.Infrastructure.Persistence.Models;

namespace CloudKnowledge.Infrastructure.Persistence;

public sealed class CloudKnowledgeDbContext : DbContext
{
    public CloudKnowledgeDbContext(
        DbContextOptions<CloudKnowledgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks =>
    Set<DocumentChunk>();

    public DbSet<DocumentChunkEmbeddingRow> DocumentChunkEmbeddings =>
        Set<DocumentChunkEmbeddingRow>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CloudKnowledgeDbContext).Assembly);
    }
}