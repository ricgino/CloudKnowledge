using CloudKnowledge.Domain.Documents;

namespace CloudKnowledge.Domain.Tests.Documents;

public sealed class DocumentTests
{
    [Fact]
    public void Create_ShouldCreatePendingDocument()
    {
        var document = Document.Create(
            "architecture.pdf",
            "application/pdf");

        Assert.Equal(DocumentStatus.Pending, document.Status);
    }

    [Fact]
    public void MarkAsProcessing_WhenPending_ShouldChangeStatusToProcessing()
    {
        var document = Document.Create(
            "architecture.pdf",
            "application/pdf");

        document.MarkAsProcessing();

        Assert.Equal(DocumentStatus.Processing, document.Status);
    }

    [Fact]
    public void MarkAsReady_WhenPending_ShouldThrow()
    {
        var document = Document.Create(
            "architecture.pdf",
            "application/pdf");

        Assert.Throws<InvalidOperationException>(
            () => document.MarkAsReady());
    }
}