namespace CloudKnowledge.Application.Teams.AddTeamMember;

public enum AddTeamMemberStatus
{
    Added = 1,
    TeamNotFoundOrNotMember = 2,
    Forbidden = 3,
    UserNotFound = 4,
    AlreadyMember = 5
}