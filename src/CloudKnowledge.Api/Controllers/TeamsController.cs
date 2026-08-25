using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Application.Teams.AddTeamMember;
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

    private readonly AddTeamMemberUseCase
        _addTeamMemberUseCase;

    public TeamsController(
        CreateTeamUseCase createTeamUseCase,
        AddTeamMemberUseCase addTeamMemberUseCase)
    {
        _createTeamUseCase =
            createTeamUseCase;

        _addTeamMemberUseCase =
            addTeamMemberUseCase;
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

    [HttpPost("{teamId:guid}/members")]
    public async Task<ActionResult<TeamMemberResponse>> AddMember(
        Guid teamId,
        [FromBody] AddTeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _addTeamMemberUseCase.ExecuteAsync(
                teamId,
                request.Email,
                cancellationToken);

        switch (result.Status)
        {
            case AddTeamMemberStatus.Added:
                return Created(
                    $"/api/teams/{teamId}/members/{result.UserId}",
                    new TeamMemberResponse(
                        result.UserId!.Value,
                        result.Email!,
                        result.Role!.Value.ToString()));

            case AddTeamMemberStatus.TeamNotFoundOrNotMember:
                return NotFound();

            case AddTeamMemberStatus.Forbidden:
                return Forbid();

            case AddTeamMemberStatus.UserNotFound:
                return NotFound(
                    new
                    {
                        message =
                            "CloudKnowledge user not found."
                    });

            case AddTeamMemberStatus.AlreadyMember:
                return Conflict(
                    new
                    {
                        message =
                            "User is already a member of this team."
                    });

            default:
                throw new InvalidOperationException(
                    "Unexpected add team member result.");
        }
    }
}