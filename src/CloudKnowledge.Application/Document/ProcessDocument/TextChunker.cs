namespace CloudKnowledge.Application.Documents.ProcessDocument;

public sealed class TextChunker
{
    private readonly int _maxCharacters;
    private readonly int _overlapCharacters;

    public TextChunker(
        int maxCharacters = 1600,
        int overlapCharacters = 200)
    {
        if (maxCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCharacters));
        }

        if (overlapCharacters < 0 ||
            overlapCharacters >= maxCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overlapCharacters));
        }

        _maxCharacters = maxCharacters;
        _overlapCharacters = overlapCharacters;
    }

    public IReadOnlyList<string> Chunk(
        string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var normalized =
            text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();

        var chunks =
            new List<string>();

        var start = 0;

        while (start < normalized.Length)
        {
            var proposedEnd =
                Math.Min(
                    start + _maxCharacters,
                    normalized.Length);

            var end =
                proposedEnd == normalized.Length
                    ? proposedEnd
                    : FindPreferredEnd(
                        normalized,
                        start,
                        proposedEnd);

            if (end <= start)
            {
                end = proposedEnd;
            }

            var chunk =
                normalized[start..end].Trim();

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            if (end >= normalized.Length)
            {
                break;
            }

            var proposedNextStart =
                Math.Max(
                    start + 1,
                    end - _overlapCharacters);

            start =
                FindNextWordBoundary(
                    normalized,
                    proposedNextStart,
                    end);

            if (start >= end)
            {
                start = end;
            }
        }

        return chunks;
    }

    private static int FindPreferredEnd(
        string text,
        int start,
        int proposedEnd)
    {
        const int searchWindow = 350;

        var searchStart =
            Math.Max(
                start + 1,
                proposedEnd - searchWindow);

        // Prefer paragraph boundaries.
        for (var index = proposedEnd - 2;
             index >= searchStart;
             index--)
        {
            if (text[index] == '\n' &&
                text[index + 1] == '\n')
            {
                return index + 2;
            }
        }

        // Then sentence boundaries.
        for (var index = proposedEnd - 2;
             index >= searchStart;
             index--)
        {
            var character =
                text[index];

            if ((character == '.' ||
                 character == '!' ||
                 character == '?') &&
                char.IsWhiteSpace(
                    text[index + 1]))
            {
                return index + 1;
            }
        }

        // Then line boundaries.
        for (var index = proposedEnd - 1;
             index >= searchStart;
             index--)
        {
            if (text[index] == '\n')
            {
                return index + 1;
            }
        }

        // Finally any whitespace.
        for (var index = proposedEnd - 1;
             index >= searchStart;
             index--)
        {
            if (char.IsWhiteSpace(
                text[index]))
            {
                return index + 1;
            }
        }

        return proposedEnd;
    }

    private static int FindNextWordBoundary(
        string text,
        int proposedStart,
        int end)
    {
        var start =
            proposedStart;

        while (start < end &&
               start > 0 &&
               !char.IsWhiteSpace(
                   text[start - 1]))
        {
            start++;
        }

        while (start < end &&
               char.IsWhiteSpace(
                   text[start]))
        {
            start++;
        }

        return start;
    }
}