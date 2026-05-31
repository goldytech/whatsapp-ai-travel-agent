using System.Reflection;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Hosting;

public sealed class AlcVerticalPluginLoader(string pluginPath) : IVerticalPluginLoader
{
    private readonly string _pluginAssemblyPath = ResolvePluginAssemblyPath(pluginPath);
    private readonly object _sync = new();
    private AlcVerticalPluginLoadContext? _loadContext;
    private IVerticalPlugin? _plugin;

    public IVerticalPlugin Load()
    {
        if (_plugin is not null)
            return _plugin;

        lock (_sync)
        {
            if (_plugin is not null)
                return _plugin;

            _loadContext = new AlcVerticalPluginLoadContext(_pluginAssemblyPath);
            var assembly = _loadContext.LoadFromAssemblyPath(_pluginAssemblyPath);
            var pluginType = assembly
                .GetTypes()
                .SingleOrDefault(type =>
                    typeof(IVerticalPlugin).IsAssignableFrom(type)
                    && type is { IsClass: true, IsAbstract: false }
                    && type.GetConstructor(Type.EmptyTypes) is not null)
                ?? throw new InvalidOperationException(
                    $"No public parameterless type implementing {nameof(IVerticalPlugin)} was found in '{_pluginAssemblyPath}'.");

            _plugin = (IVerticalPlugin?)Activator.CreateInstance(pluginType)
                ?? throw new InvalidOperationException($"Failed to create the vertical plugin '{pluginType.FullName}'.");

            if (_plugin is IVerticalDeploymentValidator validator)
                validator.ValidateDeployment();

            return _plugin;
        }
    }

    private static string ResolvePluginAssemblyPath(string pluginPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginPath);

        if (File.Exists(pluginPath))
            return Path.GetFullPath(pluginPath);

        if (!Directory.Exists(pluginPath))
            throw new DirectoryNotFoundException($"The configured plugin path '{pluginPath}' does not exist.");

        var candidateAssemblies = Directory
            .EnumerateFiles(pluginPath, "AgentForge.Verticals.*.dll", SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName is not "AgentForge.Verticals.Abstractions.dll"
                    and not "AgentForge.Verticals.Hosting.dll";
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return candidateAssemblies.Count switch
        {
            1 => candidateAssemblies[0],
            0 => throw new FileNotFoundException(
                $"No vertical plugin assembly matching 'AgentForge.Verticals.*.dll' was found in '{pluginPath}'."),
            _ => throw new InvalidOperationException(
                $"Multiple vertical plugin assemblies were found in '{pluginPath}'. Configure the loader with a specific DLL path instead.")
        };
    }
}
