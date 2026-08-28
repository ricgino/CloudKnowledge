using CloudKnowledge.Application.Documents.GetDocuments;
using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;
using CloudKnowledge.Infrastructure.Documents;
using CloudKnowledge.Infrastructure.Persistence;
using CloudKnowledge.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CloudKnowledge.Infrastructure.Tests.Documents;

public sealed class DocumentLibraryFiltersTests
{
    [Fact]
    public async Task FiltersSearchAndProvenance_ShouldNeverWidenAuthorizedDocuments()
    {
        await using var postgres =
            new PostgreSqlBuilder(
                "pgvector/pgvector:0.8.6-pg18")
                .WithDatabase("cloudknowledge_document_library_test")
                .WithUsername("cloudknowledge")
                .WithPassword("cloudknowledge_test")
                .Build();

        await postgres.StartAsync();

        await using var dbContext =
            await CreateDbContextAsync(
                postgres.GetConnectionString());

        var currentUser =
            UserAccount.Create(
                "library.user@example.com",
                "Library User");

        var otherUser =
            UserAccount.Create(
                "document.owner@example.com",
                "Document Owner");

        var rai =
            Team.Create("Rai");

        var deskSharing =
            Team.Create(
                "DeskSharing",
                rai.Id);

        var booking =
            Team.Create(
                "Booking",
                rai.Id);

        var hrPortal =
            Team.Create(
                "HR Portal",
                rai.Id);

        dbContext.UserAccounts.AddRange(
            currentUser,
            otherUser);

        dbContext.Teams.AddRange(
            rai,
            deskSharing,
            booking,
            hrPortal);

        await dbContext.SaveChangesAsync();

        dbContext.TeamMembers.AddRange(
            TeamMember.Create(
                deskSharing.Id,
                currentUser.Id),
            TeamMember.Create(
                hrPortal.Id,
                currentUser.Id));

        var mine =
            CreateOwnedDocument(
                "mine-architecture.pdf",
                currentUser.Id);

        var desk =
            CreateOwnedDocument(
                "desk-manuale.pdf",
                otherUser.Id);

        var hr =
            CreateOwnedDocument(
                "HR-Handbook.PDF",
                otherUser.Id);

        var bookingSecret =
            CreateOwnedDocument(
                "booking-secret.pdf",
                otherUser.Id);

        var mixed =
            CreateOwnedDocument(
                "mixed-access.pdf",
                otherUser.Id);

        var multi =
            CreateOwnedDocument(
                "shared-multi.pdf",
                otherUser.Id);

        var outside =
            CreateOwnedDocument(
                "outside.pdf",
                otherUser.Id);

        var deskOwned =
            CreateTeamOwnedDocument(
                "desk-team-owned.pdf",
                deskSharing.Id);

        var bookingOwned =
            CreateTeamOwnedDocument(
                "booking-team-owned.pdf",
                booking.Id);

        dbContext.Documents.AddRange(
            mine,
            desk,
            hr,
            bookingSecret,
            mixed,
            multi,
            outside,
            deskOwned,
            bookingOwned);

        await dbContext.SaveChangesAsync();

        dbContext.DocumentTeamAccess.AddRange(
            DocumentTeamAccess.Create(
                desk.Id,
                deskSharing.Id),
            DocumentTeamAccess.Create(
                hr.Id,
                hrPortal.Id),
            DocumentTeamAccess.Create(
                bookingSecret.Id,
                booking.Id),
            DocumentTeamAccess.Create(
                mixed.Id,
                deskSharing.Id),
            DocumentTeamAccess.Create(
                mixed.Id,
                booking.Id),
            DocumentTeamAccess.Create(
                multi.Id,
                deskSharing.Id),
            DocumentTeamAccess.Create(
                multi.Id,
                hrPortal.Id));

        await dbContext.SaveChangesAsync();

        var repository =
            new EfDocumentAccessRepository(
                dbContext,
                new EfTeamScopeResolver(
                    dbContext));

        var allQuery =
            Query(DocumentListScope.All);

        var all =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                100,
                allQuery,
                CancellationToken.None);

        Assert.Equal(
            6,
            all.Count);
        Assert.Contains(
            all,
            document => document.Id == mine.Id);
        Assert.Contains(
            all,
            document => document.Id == desk.Id);
        Assert.Contains(
            all,
            document => document.Id == hr.Id);
        Assert.Contains(
            all,
            document => document.Id == mixed.Id);
        Assert.Contains(
            all,
            document => document.Id == multi.Id);
        Assert.Contains(
            all,
            document => document.Id == deskOwned.Id);
        Assert.DoesNotContain(
            all,
            document => document.Id == bookingSecret.Id);
        Assert.DoesNotContain(
            all,
            document => document.Id == bookingOwned.Id);
        Assert.DoesNotContain(
            all,
            document => document.Id == outside.Id);

        Assert.Equal(
            6,
            await repository.CountAsync(
                currentUser.Id,
                allQuery,
                CancellationToken.None));

        var owned =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                100,
                Query(DocumentListScope.Owned),
                CancellationToken.None);

        Assert.Single(owned);
        Assert.Equal(
            mine.Id,
            owned[0].Id);

        var deskOnlyQuery =
            Query(
                DocumentListScope.Team,
                deskSharing.Id);

        var deskOnly =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                100,
                deskOnlyQuery,
                CancellationToken.None);

        Assert.Equal(
            4,
            deskOnly.Count);
        Assert.Contains(
            deskOnly,
            document => document.Id == desk.Id);
        Assert.Contains(
            deskOnly,
            document => document.Id == mixed.Id);
        Assert.Contains(
            deskOnly,
            document => document.Id == multi.Id);
        Assert.Contains(
            deskOnly,
            document => document.Id == deskOwned.Id);

        var raiBranchQuery =
            Query(
                DocumentListScope.Team,
                rai.Id,
                includeDescendants: true);

        Assert.Equal(
            5,
            await repository.CountAsync(
                currentUser.Id,
                raiBranchQuery,
                CancellationToken.None));

        var raiFirstPage =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                2,
                raiBranchQuery,
                CancellationToken.None);

        Assert.Equal(
            2,
            raiFirstPage.Count);

        var bookingDirect =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                100,
                Query(
                    DocumentListScope.Team,
                    booking.Id),
                CancellationToken.None);

        Assert.Empty(bookingDirect);

        var search =
            await repository.GetPageAsync(
                currentUser.Id,
                0,
                100,
                Query(
                    DocumentListScope.All,
                    searchQuery: "handbook"),
                CancellationToken.None);

        Assert.Single(search);
        Assert.Equal(
            hr.Id,
            search[0].Id);

        var provenance =
            await repository.GetVisibleTeamAccessAsync(
                currentUser.Id,
                new[]
                {
                    mine.Id,
                    mixed.Id,
                    multi.Id,
                    deskOwned.Id
                },
                CancellationToken.None);

        Assert.Empty(
            provenance[mine.Id]);

        var mixedPaths =
            provenance[mixed.Id]
                .Select(team => team.Path)
                .ToArray();

        Assert.Equal(
            new[]
            {
                "Rai / DeskSharing"
            },
            mixedPaths);

        var multiPaths =
            provenance[multi.Id]
                .Select(team => team.Path)
                .OrderBy(path => path)
                .ToArray();

        Assert.Equal(
            new[]
            {
                "Rai / DeskSharing",
                "Rai / HR Portal"
            },
            multiPaths);

        Assert.Equal(
            new[]
            {
                "Rai / DeskSharing"
            },
            provenance[deskOwned.Id]
                .Select(team => team.Path)
                .ToArray());
    }

    private static GetDocumentsQuery Query(
        DocumentListScope scope,
        Guid? teamId = null,
        bool includeDescendants = false,
        string? searchQuery = null)
    {
        return new GetDocumentsQuery(
            1,
            100,
            scope,
            teamId,
            includeDescendants,
            searchQuery);
    }

    private static Document CreateOwnedDocument(
        string fileName,
        Guid ownerUserId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignOwner(
            ownerUserId);

        return document;
    }

    private static Document CreateTeamOwnedDocument(
        string fileName,
        Guid ownerTeamId)
    {
        var document =
            Document.Create(
                fileName,
                "application/pdf");

        document.AssignTeamOwner(
            ownerTeamId);

        return document;
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
            new CloudKnowledgeDbContext(
                options);

        await dbContext.Database.MigrateAsync();

        return dbContext;
    }
}
