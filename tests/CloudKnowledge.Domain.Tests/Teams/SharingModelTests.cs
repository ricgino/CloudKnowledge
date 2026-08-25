using CloudKnowledge.Domain.Documents;
using CloudKnowledge.Domain.Teams;
using CloudKnowledge.Domain.Users;

namespace CloudKnowledge.Domain.Tests.Teams;

public sealed class SharingModelTests
{
    [Fact]
    public void UserAccountCreate_ShouldCreateValidUser()
    {
        var user =
            UserAccount.Create(
                " user@example.com ",
                " User One ");

        Assert.NotEqual(
            Guid.Empty,
            user.Id);

        Assert.Equal(
            "user@example.com",
            user.Email);

        Assert.Equal(
            "User One",
            user.DisplayName);
    }

    [Fact]
    public void TeamCreate_ShouldCreateValidTeam()
    {
        var team =
            Team.Create(
                " Engineering ");

        Assert.NotEqual(
            Guid.Empty,
            team.Id);

        Assert.Equal(
            "Engineering",
            team.Name);
    }

    [Fact]
    public void TeamMemberCreate_ShouldCreateMembership()
    {
        var teamId =
            Guid.NewGuid();

        var userId =
            Guid.NewGuid();

        var membership =
            TeamMember.Create(
                teamId,
                userId,
                TeamRole.Admin);

        Assert.Equal(
            teamId,
            membership.TeamId);

        Assert.Equal(
            userId,
            membership.UserId);

        Assert.Equal(
            TeamRole.Admin,
            membership.Role);
    }

    [Fact]
    public void DocumentTeamAccessCreate_ShouldCreateSharingRelation()
    {
        var documentId =
            Guid.NewGuid();

        var teamId =
            Guid.NewGuid();

        var access =
            DocumentTeamAccess.Create(
                documentId,
                teamId);

        Assert.Equal(
            documentId,
            access.DocumentId);

        Assert.Equal(
            teamId,
            access.TeamId);
    }
}