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
}