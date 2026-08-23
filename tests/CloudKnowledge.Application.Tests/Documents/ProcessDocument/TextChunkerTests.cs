using CloudKnowledge.Application.Documents.ProcessDocument;

namespace CloudKnowledge.Application.Tests.Documents.ProcessDocument;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunk_WhenTextIsShort_ShouldReturnSingleChunk()
    {
        var chunker =
            new TextChunker(
                maxCharacters: 100,
                overlapCharacters: 20);

        var result =
            chunker.Chunk(
                "This is a short document.");

        Assert.Single(result);

        Assert.Equal(
            "This is a short document.",
            result[0]);
    }

    [Fact]
    public void Chunk_WhenTextIsLong_ShouldReturnMultipleChunks()
    {
        var chunker =
            new TextChunker(
                maxCharacters: 50,
                overlapCharacters: 10);

        var text =
            string.Join(
                " ",
                Enumerable.Repeat(
                    "CloudKnowledge document processing",
                    20));

        var result =
            chunker.Chunk(text);

        Assert.True(
            result.Count > 1);

        Assert.All(
            result,
            chunk =>
                Assert.True(
                    chunk.Length <= 50));
    }
}