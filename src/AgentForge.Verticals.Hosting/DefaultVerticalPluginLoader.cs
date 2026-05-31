using AgentForge.Verticals.Abstractions;
using AgentForge.Verticals.Travel;

namespace AgentForge.Verticals.Hosting;

public sealed class DefaultVerticalPluginLoader : IVerticalPluginLoader
{
    private readonly IVerticalPlugin _plugin = new TravelVerticalPlugin();

    public IVerticalPlugin Load() => _plugin;
}
