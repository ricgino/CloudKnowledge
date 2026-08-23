namespace CloudKnowledge.Application.Documents;

public sealed record DocumentChunkEmbedding(
    Guid ChunkId,
    Guid DocumentId,
    float[] Values);