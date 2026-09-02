using System.Text.RegularExpressions;

namespace CloudKnowledge.Application.Documents.AskDocuments;

internal static partial class AnswerContextCompressor
{
    private const int CompressionThreshold = 2600;
    private const int WindowLength = 1800;
    private const int WindowStride = 650;
    private const int MaximumSelectedWindows = 2;
    private const int MinimumDistinctMatches = 2;

    private static readonly HashSet<string> StopWords =
        new(
            new[]
            {
                "the", "and", "for", "with", "from", "that", "this", "what", "which", "who", "how", "are", "was", "were", "does", "into", "using", "only",
                "quali", "sono", "degli", "delle", "della", "dello", "dei", "del", "gli", "che", "come", "con", "per", "nel", "nella", "nelle", "film", "usando", "solo"
            },
            StringComparer.OrdinalIgnoreCase);

    public static string Compress(
        string content,
        IReadOnlyList<string> retrievalQueries)
    {
        if (string.IsNullOrWhiteSpace(content)
            || content.Length <= CompressionThreshold
            || retrievalQueries.Count == 0)
        {
            return content;
        }

        var terms =
            ExtractTerms(retrievalQueries);

        if (terms.Count < MinimumDistinctMatches)
        {
            return content;
        }

        var windows =
            BuildWindows(content);

        if (windows.Count <= 1)
        {
            return content;
        }

        var documentFrequency =
            terms.ToDictionary(
                term => term,
                term => windows.Count(
                    window => ContainsTerm(
                        window.Text,
                        term)),
                StringComparer.OrdinalIgnoreCase);

        var scored =
            windows
                .Select(
                    window =>
                    {
                        var matchedTerms =
                            terms
                                .Where(
                                    term =>
                                        ContainsTerm(
                                            window.Text,
                                            term))
                                .ToArray();

                        var weightedScore =
                            matchedTerms.Sum(
                                term =>
                                {
                                    var frequency =
                                        documentFrequency[term];

                                    return 1d +
                                           Math.Log(
                                               (windows.Count + 1d) /
                                               (frequency + 1d));
                                });

                        return new ScoredWindow(
                            window,
                            matchedTerms.Length,
                            weightedScore);
                    })
                .Where(
                    item =>
                        item.DistinctMatches >= MinimumDistinctMatches)
                .OrderByDescending(
                    item =>
                        item.WeightedScore)
                .ThenByDescending(
                    item =>
                        item.DistinctMatches)
                .ThenBy(
                    item =>
                        item.Window.Start)
                .ToArray();

        if (scored.Length == 0)
        {
            return content;
        }

        var selected =
            new List<TextWindow>();

        foreach (var candidate in scored)
        {
            if (selected.Count >= MaximumSelectedWindows)
            {
                break;
            }

            if (selected.Any(
                    existing =>
                        OverlapRatio(
                            existing,
                            candidate.Window) >= 0.50))
            {
                continue;
            }

            selected.Add(
                candidate.Window);
        }

        if (selected.Count == 0)
        {
            return content;
        }

        selected.Sort(
            (left, right) =>
                left.Start.CompareTo(
                    right.Start));

        var excerpts =
            selected
                .Select(
                    window =>
                        content.Substring(
                                window.Start,
                                window.Length)
                            .Trim())
                .Where(
                    excerpt =>
                        excerpt.Length > 0)
                .ToArray();

        if (excerpts.Length == 0)
        {
            return content;
        }

        var compressed =
            string.Join(
                "\n…\n",
                excerpts);

        return compressed.Length < content.Length
            ? compressed
            : content;
    }

    private static IReadOnlyList<string> ExtractTerms(
        IReadOnlyList<string> queries)
    {
        return queries
            .SelectMany(
                query =>
                    TokenRegex()
                        .Matches(query)
                        .Select(
                            match =>
                                match.Value.ToLowerInvariant()))
            .Where(
                token =>
                    token.Length >= 3
                    || token.Any(char.IsDigit))
            .Where(
                token =>
                    !StopWords.Contains(token))
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<TextWindow> BuildWindows(
        string content)
    {
        if (content.Length <= WindowLength)
        {
            return
            [
                new TextWindow(
                    0,
                    content.Length,
                    content)
            ];
        }

        var starts =
            new SortedSet<int>();

        for (var start = 0;
             start < content.Length;
             start += WindowStride)
        {
            starts.Add(
                Math.Min(
                    start,
                    content.Length - WindowLength));

            if (start + WindowLength >= content.Length)
            {
                break;
            }
        }

        starts.Add(
            Math.Max(
                0,
                content.Length - WindowLength));

        return starts
            .Select(
                start =>
                {
                    var length =
                        Math.Min(
                            WindowLength,
                            content.Length - start);

                    return new TextWindow(
                        start,
                        length,
                        content.Substring(
                            start,
                            length));
                })
            .ToArray();
    }

    private static bool ContainsTerm(
        string text,
        string term)
    {
        return text.Contains(
            term,
            StringComparison.OrdinalIgnoreCase);
    }

    private static double OverlapRatio(
        TextWindow left,
        TextWindow right)
    {
        var overlapStart =
            Math.Max(
                left.Start,
                right.Start);

        var overlapEnd =
            Math.Min(
                left.Start + left.Length,
                right.Start + right.Length);

        if (overlapEnd <= overlapStart)
        {
            return 0;
        }

        var overlap =
            overlapEnd - overlapStart;

        return overlap /
               (double)Math.Min(
                   left.Length,
                   right.Length);
    }

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}-]*", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    private sealed record TextWindow(
        int Start,
        int Length,
        string Text);

    private sealed record ScoredWindow(
        TextWindow Window,
        int DistinctMatches,
        double WeightedScore);
}
