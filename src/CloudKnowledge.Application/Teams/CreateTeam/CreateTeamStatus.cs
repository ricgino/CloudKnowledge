namespace CloudKnowledge.Application.Teams.CreateTeam;

public enum CreateTeamStatus
{
    Created = 1,
    ParentNotFoundOrNotMember = 2,
    Forbidden = 3
}
