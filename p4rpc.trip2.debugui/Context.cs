using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using SharedScans.Interfaces;

namespace p4rpc.trip2.debugui;

public class Context(IModLoader modLoader, IReloadedHooks hooks, IModConfig modConfig, ISharedScans sharedScans)
{
    internal IModLoader ModLoader { get; init; } = modLoader;
    internal IReloadedHooks Hooks { get; init; } = hooks;
    internal IModConfig ModConfig { get; init; } = modConfig;
    internal ISharedScans SharedScans { get; init; } = sharedScans;
}