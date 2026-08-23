using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CloudKnowledge.Application.Documents;

namespace CloudKnowledge.Infrastructure.Documents;

public sealed class DevelopmentHashEmbeddingGenerator
    : IEmbeddingGenerator
{
    private static readonly Regex TokenRegex =
        new(
            @"[\p{L}\p{N}]+",
            RegexOptions.Compiled);

    public int Dimensions { get; }

    public DevelopmentHashEmbeddingGenerator(
        int dimensions = 1536)
    {
        if (dimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensions));
        }

        Dimensions = dimensions;
    }

    public Task<IReadOnlyList<float[]>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        var embeddings =
            new float[inputs.Count][];

        for (var index = 0;
             index < inputs.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            embeddings[index] =
                Generate(
                    inputs[index]);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(
            embeddings);
    }

    private float[] Generate(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Text cannot be empty.",
                nameof(text));
        }

        var vector =
            new float[Dimensions];

        var matches =
            TokenRegex.Matches(
                text.ToLowerInvariant());

        foreach (Match match in matches)
        {
            var tokenBytes =
                Encoding.UTF8.GetBytes(
                    match.Value);

            var hash =
                SHA256.HashData(
                    tokenBytes);

            var hashValue =
                BinaryPrimitives
                    .ReadUInt32LittleEndian(
                        hash);

            var bucket =
                (int)(
                    hashValue %
                    (uint)Dimensions);

            var sign =
                (hash[4] & 1) == 0
                    ? 1f
                    : -1f;

            vector[bucket] += sign;
        }

        Normalize(vector);

        return vector;
    }

    private static void Normalize(
        float[] vector)
    {
        double sumOfSquares = 0;

        foreach (var value in vector)
        {
            sumOfSquares +=
                value * value;
        }

        var length =
            Math.Sqrt(
                sumOfSquares);

        if (length == 0)
        {
            return;
        }

        for (var index = 0;
             index < vector.Length;
             index++)
        {
            vector[index] /=
                (float)length;
        }
    }
}