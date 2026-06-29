using p4rpc.trip2.debugui.Interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using SharedScans.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.testtoolkit;

public class Context(IModLoader modLoader, IReloadedHooks hooks, IModConfig modConfig, ISharedScans sharedScans, 
    IUnrealClasses unrealClasses, IUnrealFactory unrealFactory, IUnrealMemory unrealMemory, IUnrealMethods unrealMethods,
    IUnrealNames unrealNames, IUnrealObjects unrealObjects, IUnrealSpawning unrealSpawning, IUnrealState unrealState, 
    IUnrealStrings unrealStrings, IGUIState guiState)
{
    internal IModLoader ModLoader { get; init; } = modLoader;
    internal IReloadedHooks Hooks { get; init; } = hooks;
    internal IModConfig ModConfig { get; init; } = modConfig;
    internal ISharedScans SharedScans { get; init; } = sharedScans;
    internal IUnrealClasses UnrealClasses { get; init; } = unrealClasses;
    internal IUnrealFactory UnrealFactory { get; init; } = unrealFactory;
    internal IUnrealMemory UnrealMemory { get; init; } = unrealMemory;
    internal IUnrealMethods UnrealMethods { get; init; } = unrealMethods;
    internal IUnrealNames UnrealNames { get; init; } = unrealNames;
    internal IUnrealObjects UnrealObjects { get; init; } = unrealObjects;
    internal IUnrealSpawning UnrealSpawning { get; init; } = unrealSpawning;
    internal IUnrealState UnrealState { get; init; } = unrealState;
    internal IUnrealStrings UnrealStrings { get; init; } = unrealStrings;
    internal IGUIState GUIState { get; init; } = guiState;
}