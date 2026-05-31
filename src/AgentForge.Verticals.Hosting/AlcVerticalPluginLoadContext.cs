using System.Reflection;
using System.Runtime.Loader;

namespace AgentForge.Verticals.Hosting;

internal sealed class AlcVerticalPluginLoadContext(string mainAssemblyPath) : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyNameValue = assemblyName.Name;
        if (string.IsNullOrWhiteSpace(assemblyNameValue))
            return null;

        if (IsSharedAssembly(assemblyNameValue))
            return null;

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }

    private static bool IsSharedAssembly(string assemblyName)
        => assemblyName.Equals("AgentForge.Verticals.Abstractions", StringComparison.Ordinal)
            || assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
            || assemblyName.StartsWith("ModelContextProtocol", StringComparison.Ordinal);
}
