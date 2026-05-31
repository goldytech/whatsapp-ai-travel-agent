namespace AgentForge.Verticals.Travel;

internal static class TravelDataFiles
{
    private static readonly string DataRoot = Path.Combine(
        Path.GetDirectoryName(typeof(TravelDataFiles).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve the travel vertical assembly location."),
        "Data");

    public static void EnsureDataDirectoryExists()
    {
        if (!Directory.Exists(DataRoot))
            throw new DirectoryNotFoundException($"Travel data directory was not found at '{DataRoot}'.");
    }

    public static string ReadAllText(string fileName)
    {
        EnsureDataDirectoryExists();
        var path = Path.Combine(DataRoot, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Travel data file '{fileName}' was not found at '{path}'.", path);

        return File.ReadAllText(path);
    }
}
