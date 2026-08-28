using CloudKnowledge.Api.Database;

namespace CloudKnowledge.Api.IntegrationTests.Database;

public sealed class DatabaseStartupModeTests
{
    [Fact]
    public void IsMigrationOnly_ShouldRecognizeMigrateArgument()
    {
        Assert.True(
            DatabaseStartupMode.IsMigrationOnly(
                ["--migrate"]));
    }

    [Fact]
    public void IsMigrationOnly_ShouldIgnoreNormalApplicationArguments()
    {
        Assert.False(
            DatabaseStartupMode.IsMigrationOnly(
                []));

        Assert.False(
            DatabaseStartupMode.IsMigrationOnly(
                ["--urls", "http://0.0.0.0:8080"]));
    }
}
