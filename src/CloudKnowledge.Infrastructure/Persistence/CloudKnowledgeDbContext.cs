using CloudKnowledge.Domain.Documents;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CloudKnowledgeDbContext).Assembly);
    }
}