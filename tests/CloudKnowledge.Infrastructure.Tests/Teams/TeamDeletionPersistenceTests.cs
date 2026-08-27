using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Persistence.Models;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Teams;

public sealed class TeamDeletionPersistenceTests
{
    [Fact]
    public async Task DeleteLeafAsync_ShouldDeleteOnlyTeamOwnedDocumentGraph()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_team_deletion_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var owner =
            UserAccount.Create(
                "owner@example.com",
                "Owner");

        var sourceTeam =
            Team.Create("Source");

        var preservedTeam =
            Team.Create("Preserved");

        dbContext.AddRange(
            owner,
            sourceTeam,
            preservedTeam);

        await dbContext.SaveChangesAsync();

        var sourceMembership =
            TeamMember.Create(
                sourceTeam.Id,
                owner.Id,
                TeamRole.Owner);

        var teamOwned =
            Document.Create(
                "team-owned.pdf",
                "application/pdf");

        teamOwned.AssignTeamOwner(
            sourceTeam.Id);

        var userOwned =
            Document.Create(
                "user-owned.pdf",
                "application/pdf");

        userOwned.AssignOwner(
            owner.Id);

        dbContext.AddRange(
            sourceMembership,
            teamOwned,
            userOwned);

        await dbContext.SaveChangesAsync();

        var teamOwnedChunk =
            DocumentChunk.Create(
                teamOwned.Id,
                0,
                "TEAM OWNED CONTENT");

        var userOwnedChunk =
            DocumentChunk.Create(
                userOwned.Id,
                0,
                "USER OWNED CONTENT");

        dbContext.AddRange(
            teamOwnedChunk,
            userOwnedChunk,
            DocumentTeamAccess.Create(
                teamOwned.Id,
                preservedTeam.Id),
            DocumentTeamAccess.Create(
                userOwned.Id,
                sourceTeam.Id),
            DocumentTeamAccess.Create(
                userOwned.Id,
                preservedTeam.Id));

        await dbContext.SaveChangesAsync();

        dbContext.DocumentChunkEmbeddings.AddRange(
            CreateEmbedding(teamOwnedChunk),
            CreateEmbedding(userOwnedChunk));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTeamDeletionRepository(
                dbContext);

        Assert.False(
            await repository.HasChildrenAsync(
                sourceTeam.Id,
                CancellationToken.None));

        Assert.Equal(
            new[] { teamOwned.Id },
            await repository.GetOwnedDocumentIdsAsync(
                sourceTeam.Id,
                CancellationToken.None));

        await repository.DeleteLeafAsync(
            sourceTeam.Id,
            CancellationToken.None);

        dbContext.ChangeTracker.Clear();

        Assert.False(
            await dbContext.Teams.AnyAsync(
                team => team.Id == sourceTeam.Id));

        Assert.False(
            await dbContext.TeamMembers.AnyAsync(
                member => member.TeamId == sourceTeam.Id));

        Assert.False(
            await dbContext.Documents.AnyAsync(
                document => document.Id == teamOwned.Id));

        Assert.False(
            await dbContext.DocumentChunks.AnyAsync(
                chunk => chunk.DocumentId == teamOwned.Id));

        Assert.False(
            await dbContext.DocumentChunkEmbeddings.AnyAsync(
                row => row.DocumentId == teamOwned.Id));

        Assert.False(
            await dbContext.DocumentTeamAccess.AnyAsync(
                access => access.DocumentId == teamOwned.Id));

        Assert.True(
            await dbContext.Documents.AnyAsync(
                document =>
                    document.Id == userOwned.Id &&
                    document.OwnerUserId == owner.Id));

        Assert.True(
            await dbContext.DocumentChunks.AnyAsync(
                chunk => chunk.DocumentId == userOwned.Id));

        Assert.True(
            await dbContext.DocumentChunkEmbeddings.AnyAsync(
                row => row.DocumentId == userOwned.Id));

        Assert.False(
            await dbContext.DocumentTeamAccess.AnyAsync(
                access =>
                    access.DocumentId == userOwned.Id &&
                    access.TeamId == sourceTeam.Id));

        Assert.True(
            await dbContext.DocumentTeamAccess.AnyAsync(
                access =>
                    access.DocumentId == userOwned.Id &&
                    access.TeamId == preservedTeam.Id));

        Assert.True(
            await dbContext.Teams.AnyAsync(
                team => team.Id == preservedTeam.Id));
    }

    [Fact]
    public async Task HasChildrenAsync_ShouldProtectParentTeam()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_team_children_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var parent = Team.Create("Parent");
        var child = Team.Create(
            "Child",
            parent.Id);

        dbContext.Teams.AddRange(
            parent,
            child);

        await dbContext.SaveChangesAsync();

        var repository =
            new EfTeamDeletionRepository(
                dbContext);

        Assert.True(
            await repository.HasChildrenAsync(
                parent.Id,
                CancellationToken.None));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.DeleteLeafAsync(
                parent.Id,
                CancellationToken.None));
    }

    private static DocumentChunkEmbeddingRow CreateEmbedding(
        DocumentChunk chunk)
    {
        var values = new float[768];
        values[0] = 1.0f;

        return new DocumentChunkEmbeddingRow
        {
            ChunkId = chunk.Id,
            DocumentId = chunk.DocumentId,
            Embedding = new Vector(values)
        };
    }

    private static async Task<CloudKnowledgeDbContext> CreateDbContextAsync(
        string connectionString)
    {
        var options =
            new DbContextOptionsBuilder<CloudKnowledgeDbContext>()
                .UseNpgsql(
                    connectionString,
                    npgsqlOptions =>
                        npgsqlOptions.UseVector())
                .Options;

        var dbContext =
            new CloudKnowledgeDbContext(options);

        await dbContext.Database.MigrateAsync();

        return dbContext;
    }
}
