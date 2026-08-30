using System.Text.RegularExpressions;

namespace CloudKnowledge.Application.Documents.AskDocuments;

internal static class RetrievalQueryPlanner
{
    private const int FocusWindowSize = 6;
    private const int FocusWindowOverlap = 3;

    private static readonly HashSet<string> StopWords =
        new(
            new[]
            {
                "a",
                "al",
                "alla",
                "alle",
                "anche",
                "and",
                "are",
                "come",
                "con",
                "da",
                "dal",
                "dalla",
                "de",
                "dei",
                "del",
                "della",
                "delle",
                "di",
                "disponibile",
                "disponibili",
                "documentazione",
                "eventuali",
                "explain",
                "from",
                "how",
                "il",
                "in",
                "is",
                "la",
                "le",
                "lo",
                "mantenendo",
                "nel",
                "nella",
                "nelle",
                "of",
                "on",
                "posso",
                "può",
                "puo",
                "spiega",
                "the",
                "to",
                "un",
                "una",
                "usando",
                "using",
                "what",
                "which",
                "with",
                "esclusivamente"
            },
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> CreateFocusedQueries(
        string question,
        int maximumQueries)
    {
        if (maximumQueries < 1 ||
            string.IsNullOrWhiteSpace(question))
        {
            return Array.Empty<string>();
        }

        var normalizedQuestion =
            NormalizeWhitespace(
                question);

        var significantTokens =
            ExtractTokens(
                    normalizedQuestion)
                .Where(
                    token =>
                        !StopWords.Contains(token))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (significantTokens.Length < 4)
        {
            return Array.Empty<string>();
        }

        var queries =
            new List<string>();

        AddIfUseful(
            queries,
            string.Join(
                ' ',
                significantTokens.Take(12)),
            normalizedQuestion,
            maximumQueries);

        if (significantTokens.Length > FocusWindowSize &&
            queries.Count < maximumQueries)
        {
            var anchors =
                significantTokens
                    .Where(IsTechnicalIdentifier)
                    .Take(3)
                    .ToArray();

            var starts =
                new[]
                {
                    0,
                    Math.Max(
                        0,
                        significantTokens.Length - FocusWindowSize)
                }
                .Distinct()
                .ToArray();

            foreach (var start in starts)
            {
                var window =
                    significantTokens
                        .Skip(start)
                        .Take(FocusWindowSize)
                        .ToArray();

                var focusedTokens =
                    anchors
                        .Concat(window)
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                AddIfUseful(
                    queries,
                    string.Join(
                        ' ',
                        focusedTokens),
                    normalizedQuestion,
                    maximumQueries);

                if (queries.Count >= maximumQueries)
                {
                    break;
                }
            }
        }

        if (queries.Count < maximumQueries &&
            significantTokens.Length > FocusWindowSize + FocusWindowOverlap)
        {
            var middleStart =
                Math.Max(
                    0,
                    (significantTokens.Length - FocusWindowSize) / 2);

            AddIfUseful(
                queries,
                string.Join(
                    ' ',
                    significantTokens
                        .Skip(middleStart)
                        .Take(FocusWindowSize)),
                normalizedQuestion,
                maximumQueries);
        }

        return queries;
    }

    private static IEnumerable<string> ExtractTokens(
        string value)
    {
        return Regex
            .Matches(
                value,
                @"[\p{L}\p{N}][\p{L}\p{N}_.:/+\-]*")
            .Cast<Match>()
            .Select(
                match =>
                    match.Value.Trim())
            .Where(
                token =>
                    token.Length > 1);
    }

    private static bool IsTechnicalIdentifier(
        string token)
    {
        var containsLetter =
            token.Any(char.IsLetter);

        var containsDigit =
            token.Any(char.IsDigit);

        return containsLetter && containsDigit;
    }

    private static void AddIfUseful(
        ICollection<string> queries,
        string candidate,
        string original,
        int maximumQueries)
    {
        if (queries.Count >= maximumQueries)
        {
            return;
        }

        var normalizedCandidate =
            NormalizeWhitespace(
                candidate);

        if (normalizedCandidate.Length < 8 ||
            string.Equals(
                normalizedCandidate,
                original,
                StringComparison.OrdinalIgnoreCase) ||
            queries.Contains(
                normalizedCandidate,
                StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        queries.Add(
            normalizedCandidate);
    }

    private static string NormalizeWhitespace(
        string value)
    {
        return Regex.Replace(
                value.Trim(),
                @"\s+",
                " ")
            .Trim();
    }
}
