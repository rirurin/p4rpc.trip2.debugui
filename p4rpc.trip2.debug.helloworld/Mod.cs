using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using p4rpc.trip2.debug.helloworld.Template;
using p4rpc.trip2.debug.helloworld.Configuration;
using p4rpc.trip2.debugui.Interfaces;
using riri.yamlscans.ReloadedII;
using RyoTune.Reloaded;

namespace p4rpc.trip2.debug.helloworld;

public class Mod : ModBase
{
    private readonly IModLoader _modLoader;
    private readonly IReloadedHooks? _hooks;
    private readonly ILogger _logger;
    private readonly IMod _owner;
    private Config _configuration;
    private readonly IModConfig _modConfig;

    private IGUIState _guiState;
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
        _app = new();
        _guiState = YamlScans.GetDependency<IGUIState>();
        _guiState.Register(_app);
    }

    #region Standard Overrides

    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
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