namespace CloudKnowledge.Api.Database;

public static class DatabaseStartupMode
{
    public const string MigrateArgument = "--migrate";

    public static bool IsMigrationOnly(
        IReadOnlyList<string> args)
    {
        return args.Any(
            argument =>
                string.Equals(
                    argument,
                    MigrateArgument,
                    StringComparison.OrdinalIgnoreCase));
    }

    public static string[] RemoveMigrationArgument(
        IReadOnlyList<string> args)
    {
        return args
            .Where(
                argument =>
                    !string.Equals(
                        argument,
                        MigrateArgument,
                        StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
