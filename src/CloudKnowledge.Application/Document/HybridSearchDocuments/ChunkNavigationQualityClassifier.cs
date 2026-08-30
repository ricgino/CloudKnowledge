using System.Text.RegularExpressions;

namespace CloudKnowledge.Application.Documents.HybridSearchDocuments;

public sealed partial class ChunkNavigationQualityClassifier
{
    private const double NavigationPenaltyMultiplier =
        0.80;

    public bool IsNavigationLike(
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var lines =
            content
                .Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();

        var signals = 0;

        if (NavigationHeadingRegex()
            .IsMatch(content))
        {
            signals++;
        }

        var dottedLeaderLines =
            lines.Count(
                line =>
                    DottedLeaderPageRegex()
                        .IsMatch(line));

        if (dottedLeaderLines >= 3)
        {
            signals++;
        }

        var headingPageLines =
            lines.Count(
                line =>
                    line.Length <= 120
                    && HeadingPageRegex()
                        .IsMatch(line)
                    && SentencePunctuationRegex()
                        .Matches(line)
                        .Count <= 1);

        if (headingPageLines >= 4)
        {
            signals++;
        }

        var sentencePunctuation =
            SentencePunctuationRegex()
                .Matches(content)
                .Count;

        if (lines.Length >= 5
            && headingPageLines >= 3
            && sentencePunctuation <= 1)
        {
            signals++;
        }

        return signals >= 2;
    }

    public double ApplyPenalty(
        double score,
        bool isNavigationLike)
    {
        return isNavigationLike
            ? score * NavigationPenaltyMultiplier
            : score;
    }

    [GeneratedRegex(
        @"\b(table\s+of\s+contents|contents|index|indice|sommario)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NavigationHeadingRegex();

    [GeneratedRegex(
        @"\.{4,}\s*\d+\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DottedLeaderPageRegex();

    [GeneratedRegex(
        @"^.{2,100}(?:\.{2,}\s*)?\d+\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPageRegex();

    [GeneratedRegex(
        @"[.!?]",
        RegexOptions.CultureInvariant)]
    private static partial Regex SentencePunctuationRegex();
}
