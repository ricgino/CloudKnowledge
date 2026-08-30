using CloudKnowledge.Api.Contracts.Ask;
using CloudKnowledge.Api.Contracts.Documents;
using CloudKnowledge.Application.Documents.AskDocuments;
using CloudKnowledge.Application.Documents.HybridSearchDocuments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/ask")]
public sealed class AskController
    : ControllerBase
{
    private const int MaximumDiagnosticCandidates =
        8;

    private readonly AskDocumentsUseCase
        _askDocumentsUseCase;

    public AskController(
        AskDocumentsUseCase askDocumentsUseCase)
    {
        _askDocumentsUseCase =
            askDocumentsUseCase;
    }

    [HttpPost]
    public async Task<ActionResult<AskDocumentsResponse>> Ask(
        [FromBody] AskDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        if (!RetrievalScopeRequestParser.TryParse(
                request.Scope,
                request.TeamId,
                request.IncludeDescendants,
                out var scope,
                out var errorMessage))
        {
            return BadRequest(
                new
                {
                    message = errorMessage
                });
        }

        var result =
            await _askDocumentsUseCase.ExecuteAsync(
                request.Question,
                request.Take,
                scope,
                cancellationToken);

        var sources =
            result.Sources
                .Select(
                    source =>
                        new AskDocumentSourceResponse(
                            source.Label,
                            source.DocumentId,
                            source.ChunkId,
                            source.Position,
                            source.Content,
                            source.Similarity))
                .ToArray();

        var retrievalDiagnostics =
            result.RetrievalDiagnostics
                .Select(
                    MapDiagnostics)
                .ToArray();

        return Ok(
            new AskDocumentsResponse(
                result.Answer,
                sources,
                result.RetrievalQueries,
                retrievalDiagnostics));
    }

    private static AskRetrievalQueryDiagnosticsResponse MapDiagnostics(
        AskRetrievalQueryDiagnostics diagnostics)
    {
        var semanticCandidates =
            diagnostics.SemanticCandidates
                .Take(
                    MaximumDiagnosticCandidates)
                .Select(
                    candidate =>
                        new AskRetrievalCandidateResponse(
                            candidate.DocumentId,
                            candidate.ChunkId,
                            candidate.Rank,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null))
                .ToArray();

        var lexicalCandidates =
            diagnostics.LexicalCandidates
                .Take(
                    MaximumDiagnosticCandidates)
                .Select(
                    candidate =>
                        new AskRetrievalCandidateResponse(
                            candidate.DocumentId,
                            candidate.ChunkId,
                            candidate.Rank,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            null))
                .ToArray();

        var hybridCandidates =
            diagnostics.HybridCandidates
                .Take(
                    MaximumDiagnosticCandidates)
                .Select(
                    candidate =>
                        new AskRetrievalCandidateResponse(
                            candidate.DocumentId,
                            candidate.ChunkId,
                            null,
                            candidate.SemanticRank,
                            candidate.LexicalRank,
                            candidate.FusedScore,
                            candidate.AdjustedFusedScore,
                            MapChannel(
                                candidate.Channel),
                            candidate.NavigationPenalty,
                            candidate.Selected))
                .ToArray();

        return new AskRetrievalQueryDiagnosticsResponse(
            diagnostics.Kind switch
            {
                AskRetrievalQueryKind.Original =>
                    "original",
                AskRetrievalQueryKind.Focused =>
                    "focused",
                _ =>
                    throw new ArgumentOutOfRangeException(
                        nameof(diagnostics),
                        diagnostics.Kind,
                        "Unsupported retrieval query kind.")
            },
            diagnostics.Query,
            semanticCandidates,
            lexicalCandidates,
            hybridCandidates);
    }

    private static string MapChannel(
        HybridRetrievalChannel channel)
    {
        return channel switch
        {
            HybridRetrievalChannel.Semantic =>
                "semantic",
            HybridRetrievalChannel.Lexical =>
                "lexical",
            HybridRetrievalChannel.Both =>
                "both",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(channel),
                    channel,
                    "Unsupported retrieval channel.")
        };
    }
}
