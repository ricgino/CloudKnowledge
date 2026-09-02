using CloudKnowledge.Application.Documents.HybridSearchDocuments;

namespace CloudKnowledge.Application.Tests.Documents.HybridSearchDocuments;

public sealed class ChunkNavigationQualityClassifierTests
{
    [Fact]
    public void IsNavigationLike_ShouldFlagDenseTableOfContents()
    {
        var content =
            """
            Table of contents
            3 Mechanical installation............................13
            4 Electrical installation............................15
            5 Technical data......................................17
            Ratings...............................................17
            Definitions...........................................32
            """;

        var classifier =
            new ChunkNavigationQualityClassifier();

        Assert.True(
            classifier.IsNavigationLike(
                content));
    }

    [Fact]
    public void IsNavigationLike_ShouldNotFlagNormalTechnicalPassageWithHeading()
    {
        var content =
            """
            Altitude derating
            The rated output current must be reduced when the installation altitude exceeds the reference altitude. Calculate the permitted output current according to the stated derating factor and the actual installation conditions.
            """;

        var classifier =
            new ChunkNavigationQualityClassifier();

        Assert.False(
            classifier.IsNavigationLike(
                content));
    }

    [Fact]
    public void ApplyPenalty_ShouldReduceButNotRemoveNavigationCandidate()
    {
        var classifier =
            new ChunkNavigationQualityClassifier();

        var adjusted =
            classifier.ApplyPenalty(
                1.0,
                isNavigationLike: true);

        Assert.Equal(
            0.8,
            adjusted,
            precision: 6);

        Assert.True(
            adjusted > 0.0);
    }
}
