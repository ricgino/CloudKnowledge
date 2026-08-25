using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Application.Teams.CreateTeam;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;

namespace CloudKnowledge.Api.Controllers;

[Authorize]
[RequiredScope("access_as_user")]
[ApiController]
[Route("api/teams")]
public sealed class TeamsController
    : ControllerBase
{
    private readonly CreateTeamUseCase
        _createTeamUseCase;

    public TeamsController(
        CreateTeamUseCase createTeamUseCase)
    {
        _createTeamUseCase =
            createTeamUseCase;
    }

    [HttpPost]
    public async Task<ActionResult<TeamResponse>> Create(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _createTeamUseCase.ExecuteAsync(
                request.Name,
                cancellationToken);

        return Created(
            $"/api/teams/{result.Id}",
            new TeamResponse(
                result.Id,
                result.Name,
                result.Role.ToString()));
    }
}