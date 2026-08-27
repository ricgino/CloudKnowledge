using CloudKnowledge.Domain.Teams;

namespace CloudKnowledge.Domain.Tests.Teams;

public sealed class TeamHierarchyTests
{
    [Fact]
    public void Create_WithoutParent_CreatesRootTeam()
    {
        var team = Team.Create(
            "Rai");

        Assert.Null(
            team.ParentTeamId);
    }

    [Fact]
    public void Create_WithParent_StoresParentTeamId()
    {
        var parentId =
            Guid.NewGuid();

        var team = Team.Create(
            "DeskSharing",
            parentId);

        Assert.Equal(
            parentId,
            team.ParentTeamId);
    }
}
