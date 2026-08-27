using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Notifications;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace CloudKnowledge.Infrastructure.Persistence;

public sealed class CloudKnowledgeDbContext : DbContext
{
    public CloudKnowledgeDbContext(
        DbContextOptions<CloudKnowledgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents =>
        Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks =>
        Set<DocumentChunk>();

    public DbSet<DocumentChunkEmbeddingRow> DocumentChunkEmbeddings =>
        Set<DocumentChunkEmbeddingRow>();

    public DbSet<UserAccount> UserAccounts =>
        Set<UserAccount>();

    public DbSet<Team> Teams =>
        Set<Team>();

    public DbSet<TeamMember> TeamMembers =>
        Set<TeamMember>();

    public DbSet<DocumentTeamAccess> DocumentTeamAccess =>
        Set<DocumentTeamAccess>();

    public DbSet<Notification> Notifications =>
        Set<Notification>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CloudKnowledgeDbContext).Assembly);
    }
}
