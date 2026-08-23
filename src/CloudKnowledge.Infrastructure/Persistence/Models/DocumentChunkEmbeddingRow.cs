using Pgvector;

namespace CloudKnowledge.Infrastructure.Persistence.Models;

public sealed class DocumentChunkEmbeddingRow
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public Vector Embedding { get; set; } = null!;
}