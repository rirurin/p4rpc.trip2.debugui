using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using p4rpc.trip2.debug.testtoolkit.Template;
using p4rpc.trip2.debug.testtoolkit.Configuration;
using p4rpc.trip2.debugui.Interfaces;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;
using SharedScans.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Interfaces;
using UnrealEssentials.Interfaces;
using IUnrealMemory = UE.Toolkit.Interfaces.IUnrealMemory;

namespace p4rpc.trip2.debug.testtoolkit;

public class Mod : ModBase
{
    private readonly IModLoader _modLoader;
    private readonly IReloadedHooks? _hooks;
    private readonly ILogger _logger;
    private readonly IMod _owner;
    private Config _configuration;
    private readonly IModConfig _modConfig;

    private Context _context;
    private App _app;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;
        
        Project.Initialize(_modConfig, _modLoader, _logger, false);
        Log.LogLevel = _configuration.LogLevel;
        YamlScans.Initialize(_modConfig, _modLoader);
        var sharedScans = YamlScans.GetDependency<ISharedScans>();
        var unrealClasses = YamlScans.GetDependency<IUnrealClasses>();
        var unrealFactory = YamlScans.GetDependency<IUnrealFactory>();
        var unrealMemory = YamlScans.GetDependency<IUnrealMemory>();
        var unrealMethods = YamlScans.GetDependency<IUnrealMethods>();
        var unrealNames = YamlScans.GetDependency<IUnrealNames>();
        var unrealObjects = YamlScans.GetDependency<IUnrealObjects>();
        var unrealSpawning = YamlScans.GetDependency<IUnrealSpawning>();
        var unrealState = YamlScans.GetDependency<IUnrealState>();
        var unrealStrings = YamlScans.GetDependency<IUnrealStrings>();
        var guiState = YamlScans.GetDependency<IGUIState>();
        var unrealEssentials = YamlScans.GetDependency<IUnrealEssentials>();
        _context = new(_modLoader, _hooks!, _modConfig, sharedScans, unrealClasses, unrealFactory, unrealMemory,
            unrealMethods, unrealNames, unrealObjects, unrealSpawning, unrealState, unrealStrings, guiState, unrealEssentials);
        _app = new(_context);
        _context.GUIState.Register(_app);

    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        _configuration = configuration;
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");
    }

    #endregion

    #region For Exports, Serialization etc.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod()
    {
    }
#pragma warning restore CS8618

    #endregion
}