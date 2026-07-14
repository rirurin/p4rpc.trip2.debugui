using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using p4rpc.trip2.debug.uobjectviewer.Template;
using p4rpc.trip2.debug.uobjectviewer.Configuration;
using p4rpc.trip2.debugui.Interfaces;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;
using SharedScans.Interfaces;
using UE.Toolkit.Core.Types.Unreal.Factories;
using UE.Toolkit.Interfaces;

namespace p4rpc.trip2.debug.uobjectviewer;

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
        var unrealObjects = YamlScans.GetDependency<IUnrealObjects>();
        var unrealFactory = YamlScans.GetDependency<IUnrealFactory>();
        var unrealStrings = YamlScans.GetDependency<IUnrealStrings>();
        var guiState = YamlScans.GetDependency<IGUIState>();
        var unrealMemory = YamlScans.GetDependency<IUnrealMemory>();
        _context = new(_modLoader, _hooks!, _modConfig, sharedScans, unrealObjects, unrealFactory, guiState, 
            unrealStrings, unrealMemory);
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