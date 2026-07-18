using p4rpc.trip2.debugui.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using SharedScans.Interfaces;
using UE.Toolkit.Core.Types.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

public class Context(IModLoader modLoader, IReloadedHooks hooks, IModConfig modConfig, ISharedScans sharedScans, 
    IUnrealObjects unrealObjects, IUnrealFactory unrealFactory, IGUIState guiState, IUnrealStrings unrealStrings,
    IUnrealMemory unrealMemory, IUnrealClasses unrealClasses)
{
    internal IModLoader ModLoader { get; init; } = modLoader;
    internal IReloadedHooks Hooks { get; init; } = hooks;
    internal IModConfig ModConfig { get; init; } = modConfig;
    internal ISharedScans SharedScans { get; init; } = sharedScans;
    internal IUnrealObjects UnrealObjects { get; init; } = unrealObjects;
    internal IUnrealFactory UnrealFactory { get; init; } = unrealFactory;
    internal IGUIState GUIState { get; init; } = guiState;
    internal IUnrealStrings UnrealStrings { get; init; } = unrealStrings;
    internal IUnrealMemory UnrealMemory { get; init; } = unrealMemory;
    internal IUnrealClasses UnrealClasses { get; init; } = unrealClasses;
}