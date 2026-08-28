namespace CloudKnowledge.Application.Teams.DeleteTeam;

public enum DeleteTeamStatus
{
    Deleted = 1,
    NotFound = 2,
    Forbidden = 3,
    HasChildren = 4
}
