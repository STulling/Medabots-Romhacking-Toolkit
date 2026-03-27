namespace Medabots.Rom.Tests;

internal static class TestRomLocator
{
    public static string FindWorkspaceRom()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Medabots Rokusho Version (E).gba");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find the local Rokusho ROM used for integration testing.");
    }
}
