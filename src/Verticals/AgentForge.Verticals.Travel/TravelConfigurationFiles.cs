using Microsoft.Extensions.Configuration;

namespace AgentForge.Verticals.Travel;

internal static class TravelConfigurationFiles
{
    private const string ConfigurationDirectoryName = "Configuration";
    private const string CustomerProfileFileName = "customer-profile.json";

    private static readonly string ConfigurationRoot = Path.Combine(
        Path.GetDirectoryName(typeof(TravelConfigurationFiles).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve the travel vertical assembly location."),
        ConfigurationDirectoryName);

    public static void AddConfigurationSources(IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        EnsureDefaultFilesExist();
        configuration.AddJsonFile(Path.Combine(ConfigurationRoot, CustomerProfileFileName), optional: false, reloadOnChange: false);

        if (TryGetExternalConfigurationRoot(configuration, out var externalConfigurationRoot))
        {
            var externalProfilePath = Path.Combine(externalConfigurationRoot, CustomerProfileFileName);
            if (!File.Exists(externalProfilePath))
                throw new FileNotFoundException(
                    $"Customer profile file was not found at '{externalProfilePath}'. " +
                    $"Set CUSTOMER_CONFIG_PATH to a directory that contains '{CustomerProfileFileName}'.",
                    externalProfilePath);

            configuration.AddJsonFile(externalProfilePath, optional: false, reloadOnChange: false);
        }
    }

    public static void EnsureDefaultFilesExist()
    {
        if (!Directory.Exists(ConfigurationRoot))
            throw new DirectoryNotFoundException($"Travel configuration directory was not found at '{ConfigurationRoot}'.");

        EnsureFileExists(Path.Combine(ConfigurationRoot, CustomerProfileFileName));
        EnsureFileExists(Path.Combine(ConfigurationRoot, "prompt.md"));
    }

    public static string ResolvePromptPath(IConfiguration configuration, string promptFileName)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptFileName);

        if (Path.IsPathRooted(promptFileName) || promptFileName.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.None)
            .Any(segment => segment.Equals("..", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Prompt file '{promptFileName}' must stay inside the configuration folder.");
        }

        if (TryGetExternalConfigurationRoot(configuration, out var externalConfigurationRoot))
        {
            var externalPromptPath = Path.Combine(externalConfigurationRoot, promptFileName);
            if (File.Exists(externalPromptPath))
                return externalPromptPath;
        }

        var defaultPromptPath = Path.Combine(ConfigurationRoot, promptFileName);
        EnsureFileExists(defaultPromptPath);
        return defaultPromptPath;
    }

    private static bool TryGetExternalConfigurationRoot(IConfiguration configuration, out string externalConfigurationRoot)
    {
        var configuredPath = configuration["CUSTOMER_CONFIG_PATH"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            externalConfigurationRoot = string.Empty;
            return false;
        }

        externalConfigurationRoot = Path.GetFullPath(configuredPath);
        if (!Directory.Exists(externalConfigurationRoot))
            throw new DirectoryNotFoundException(
                $"CUSTOMER_CONFIG_PATH points to '{externalConfigurationRoot}', but that directory does not exist.");

        return true;
    }

    private static void EnsureFileExists(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Travel configuration file was not found at '{path}'.", path);
    }
}
