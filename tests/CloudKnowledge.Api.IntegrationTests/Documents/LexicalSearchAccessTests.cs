using CloudKnowledge.Application.Documents.SearchDocuments;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Api.IntegrationTests.Documents;

public sealed class LexicalSearchAccessTests
{
    [Fact]
    public async Task SearchAccessibleAsync_ShouldFindExactTechnicalEvidenceWithoutLeakingPrivateDocuments()
    {
        await using var fixture =
            await LexicalFixture.CreateAsync();

        var repository =
            new EfDocumentLexicalSearchRepository(
                fixture.DbContext,
                new EfTeamScopeResolver(
                    fixture.DbContext));

        var results =
            await repository.SearchAccessibleAsync(
                fixture.Bob.Id,
                "rated output current altitude derating",
                10,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Contains(
            results,
            result =>
                result.ChunkId ==
                fixture.AccessibleChunk.Id);

        Assert.DoesNotContain(
            results,
            result =>
                result.DocumentId ==
                fixture.PrivateDocument.Id);
    }

    [Fact]
    public async Task SearchAccessibleAsync_ShouldRespectTeamScope()
    {
        await using var fixture =
            await LexicalFixture.CreateAsync();

        var repository =
            new EfDocumentLexicalSearchRepository(
                fixture.DbContext,
                new EfTeamScopeResolver(
                    fixture.DbContext));

        var results =
            await repository.SearchAccessibleAsync(
                fixture.Bob.Id,
                "rated output current altitude derating",
                10,
                DocumentRetrievalScope.ForTeam(
                    fixture.Engineering.Id,
                    includeDescendants: false),
                CancellationToken.None);

        Assert.Contains(
            results,
            result =>
                result.ChunkId ==
                fixture.AccessibleChunk.Id);

        Assert.DoesNotContain(
            results,
            result =>
                result.DocumentId ==
                fixture.BobPrivateDocument.Id);
    }

    [Fact]
    public async Task Migration_ShouldCreateGeneratedSearchVectorAndGinIndex_ForExistingChunks()
    {
        await using var fixture =
            await LexicalFixture.CreateAsync();

        await using var connection =
            new NpgsqlConnection(
                fixture.ConnectionString);

        await connection.OpenAsync();

        await using var columnCommand =
            new NpgsqlCommand(
                """
                SELECT data_type
                FROM information_schema.columns
                WHERE table_name = 'document_chunks'
                  AND column_name = 'search_vector';
                """,
                connection);

        var dataType =
            (string?)await columnCommand.ExecuteScalarAsync();

        Assert.Equal(
            "USER-DEFINED",
            dataType);

        await using var indexCommand =
            new NpgsqlCommand(
                """
                SELECT indexdef
                FROM pg_indexes
                WHERE tablename = 'document_chunks'
                  AND indexname = 'IX_document_chunks_search_vector';
                """,
                connection);

        var indexDefinition =
            (string?)await indexCommand.ExecuteScalarAsync();

        Assert.NotNull(
            indexDefinition);

        Assert.Contains(
            "USING gin",
            indexDefinition,
            StringComparison.OrdinalIgnoreCase);

        var repository =
            new EfDocumentLexicalSearchRepository(
                fixture.DbContext,
                new EfTeamScopeResolver(
                    fixture.DbContext));

        var results =
            await repository.SearchAccessibleAsync(
                fixture.Bob.Id,
                "rated output current altitude derating",
                10,
                DocumentRetrievalScope.All,
                CancellationToken.None);

        Assert.Contains(
            results,
            result =>
                result.ChunkId ==
                fixture.AccessibleChunk.Id);
    }

    private sealed class LexicalFixture
        : IAsyncDisposable
    {
        private readonly PostgreSqlContainer
            _postgres;

        public CloudKnowledgeDbContext DbContext { get; }
        public string ConnectionString { get; }
        public UserAccount Bob { get; }
        public Team Engineering { get; }
        public Document PrivateDocument { get; }
        public Document BobPrivateDocument { get; }
        public DocumentChunk AccessibleChunk { get; }

        private LexicalFixture(
            PostgreSqlContainer postgres,
            CloudKnowledgeDbContext dbContext,
            string connectionString,
            UserAccount bob,
            Team engineering,
            Document privateDocument,
            Document bobPrivateDocument,
            DocumentChunk accessibleChunk)
        {
            _postgres =
                postgres;

            DbContext =
                dbContext;

            ConnectionString =
                connectionString;

            Bob =
                bob;

            Engineering =
                engineering;

            PrivateDocument =
                privateDocument;

            BobPrivateDocument =
                bobPrivateDocument;

            AccessibleChunk =
                accessibleChunk;
        }

        public static async Task<LexicalFixture> CreateAsync()
        {
            var postgres =
                new PostgreSqlBuilder(
                    "pgvector/pgvector:0.8.6-pg18")
                    .WithDatabase(
                        "cloudknowledge_lexical_search_test")
                    .WithUsername(
                        "cloudknowledge")
                    .WithPassword(
                        "cloudknowledge_test")
                    .Build();

            await postgres.StartAsync();

            var connectionString =
                postgres.GetConnectionString();

            var options =
                new DbContextOptionsBuilder<CloudKnowledgeDbContext>()
                    .UseNpgsql(
                        connectionString,
                        npgsqlOptions =>
                            npgsqlOptions.UseVector())
                    .Options;

            var dbContext =
                new CloudKnowledgeDbContext(
                    options);

            await dbContext.Database
                .MigrateAsync();

            var alice =
                UserAccount.Create(
                    "alice-lexical@example.com",
                    "Alice");

            var bob =
                UserAccount.Create(
                    "bob-lexical@example.com",
                    "Bob");

            var engineering =
                Team.Create(
                    "Engineering lexical");

            var aliceEngineeringMembership =
                TeamMember.Create(
                    engineering.Id,
                    alice.Id,
                    TeamRole.Owner);

            var bobEngineeringMembership =
                TeamMember.Create(
                    engineering.Id,
                    bob.Id,
                    TeamRole.Member);

            var engineeringDocument =
                Document.Create(
                    "engineering-lexical.pdf",
                    "application/pdf");

            engineeringDocument.AssignTeamOwner(
                engineering.Id);

            var privateDocument =
                Document.Create(
                    "alice-private-lexical.pdf",
                    "application/pdf");

            privateDocument.AssignOwner(
                alice.Id);

            var bobPrivateDocument =
                Document.Create(
                    "bob-private-lexical.pdf",
                    "application/pdf");

            bobPrivateDocument.AssignOwner(
                bob.Id);

            var accessibleChunk =
                DocumentChunk.Create(
                    engineeringDocument.Id,
                    0,
                    "At high installation altitude the rated output current requires derating.");

            var privateChunk =
                DocumentChunk.Create(
                    privateDocument.Id,
                    0,
                    "High altitude rated output current derating confidential rule.");

            var bobPrivateChunk =
                DocumentChunk.Create(
                    bobPrivateDocument.Id,
                    0,
                    "Rated output current altitude derating personal engineering notes.");

            dbContext.AddRange(
                alice,
                bob,
                engineering,
                aliceEngineeringMembership,
                bobEngineeringMembership,
                engineeringDocument,
                privateDocument,
                bobPrivateDocument,
                accessibleChunk,
                privateChunk,
                bobPrivateChunk);

            await dbContext.SaveChangesAsync();

            return new LexicalFixture(
                postgres,
                dbContext,
                connectionString,
                bob,
                engineering,
                privateDocument,
                bobPrivateDocument,
                accessibleChunk);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _postgres.DisposeAsync();
        }
    }
}
