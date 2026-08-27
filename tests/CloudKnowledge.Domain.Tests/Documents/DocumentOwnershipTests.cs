using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Domain.Tests.Documents;

public sealed class DocumentOwnershipTests
{
    [Fact]
    public void AssignOwner_WhenDocumentHasNoOwner_ShouldAssignOwner()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        var userId =
            Guid.NewGuid();

        document.AssignOwner(
            userId);

        Assert.Equal(
            userId,
            document.OwnerUserId);
    }

    [Fact]
    public void AssignOwner_WhenUserIdIsEmpty_ShouldThrow()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        Assert.Throws<ArgumentException>(
            () =>
                document.AssignOwner(
                    Guid.Empty));
    }

    [Fact]
    public void AssignOwner_WhenDocumentAlreadyHasAnotherOwner_ShouldThrow()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        document.AssignOwner(
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () =>
                document.AssignOwner(
                    Guid.NewGuid()));
    }

    [Fact]
    public void AssignTeamOwner_WhenDocumentHasNoOwner_ShouldAssignOnlyTeamOwner()
    {
        var document =
            Document.Create(
                "team-handbook.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var teamId =
            Guid.NewGuid();

        document.AssignTeamOwner(
            teamId);

        Assert.Equal(
            teamId,
            document.OwnerTeamId);
        Assert.Null(
            document.OwnerUserId);
    }

    [Fact]
    public void AssignTeamOwner_WhenSameTeamIsAssignedTwice_ShouldBeIdempotent()
    {
        var document =
            Document.Create(
                "team-notes.txt",
                "text/plain");

        var teamId =
            Guid.NewGuid();

        document.AssignTeamOwner(
            teamId);
        document.AssignTeamOwner(
            teamId);

        Assert.Equal(
            teamId,
            document.OwnerTeamId);
        Assert.Null(
            document.OwnerUserId);
    }

    [Fact]
    public void AssignTeamOwner_WhenTeamIdIsEmpty_ShouldThrow()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        Assert.Throws<ArgumentException>(
            () =>
                document.AssignTeamOwner(
                    Guid.Empty));
    }

    [Fact]
    public void AssignTeamOwner_WhenUserOwnerAlreadyExists_ShouldThrow()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        document.AssignOwner(
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () =>
                document.AssignTeamOwner(
                    Guid.NewGuid()));
    }

    [Fact]
    public void AssignOwner_WhenTeamOwnerAlreadyExists_ShouldThrow()
    {
        var document =
            Document.Create(
                "document.pdf",
                "application/pdf");

        document.AssignTeamOwner(
            Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(
            () =>
                document.AssignOwner(
                    Guid.NewGuid()));
    }
}
