using CloudKnowledge.Api.Contracts.Teams;
using CloudKnowledge.Application.Teams.AddTeamMember;
using CloudKnowledge.Application.Teams.CreateTeam;
using CloudKnowledge.Application.Teams.GetTeams;
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

    private readonly GetTeamsUseCase
        _getTeamsUseCase;

    public TeamsController(
        CreateTeamUseCase createTeamUseCase,
        AddTeamMemberUseCase addTeamMemberUseCase,
        GetTeamsUseCase getTeamsUseCase)
    {
        _createTeamUseCase =
            createTeamUseCase;

        _addTeamMemberUseCase =
            addTeamMemberUseCase;

        _getTeamsUseCase =
            getTeamsUseCase;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var results =
            await _getTeamsUseCase.ExecuteAsync(
                cancellationToken);

        var response =
            results
                .Select(result =>
                    new TeamResponse(
                        result.Id,
                        result.Name,
                        result.Role.ToString()))
                .ToArray();

        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<TeamResponse>> Create(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await _createTeamUseCase.ExecuteAsync(
                request.Name,
                request.ParentTeamId,
                cancellationToken);

        switch (result.Status)
        {
            case CreateTeamStatus.Created:
                return Created(
                    $"/api/teams/{result.Id}",
                    new TeamResponse(
                        result.Id!.Value,
                        result.Name!,
                        result.Role!.Value.ToString()));

            case CreateTeamStatus.ParentNotFoundOrNotMember:
                return NotFound();

            case CreateTeamStatus.Forbidden:
                return Forbid();

            default:
                throw new InvalidOperationException(
                    "Unexpected create team result.");
        }
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
